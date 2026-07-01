using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Veeling.CLI.Exceptions;
using Veeling.CLI.Providers;
using Veeling.Models;
using Veeling.Models.Schema;

namespace Veeling.CLI;

public class TranslationJob
{
    private const int MaxDiagnosticExcerptLength = 280;
    private const int MaxFieldListInDiagnostics = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Lazy<VSchema> schema;
    private readonly IProjectDataSession session;
    private readonly Lazy<ILLMProvider> llmProvider;
    private readonly Language from;
    private readonly Language to;
    private readonly IProviderAuthFailureClassifier authFailureClassifier;
    private readonly GlossaryModel? glossary;

    private DataRetrieveResult[]? source;
    private Dictionary<string, string>? variables;

    public TranslationJob(
        IProjectDataSession session,
        Func<ILLMProvider> llmProviderFactory,
        Project project,
        string schemaName,
        Language from,
        Language to,
        IProviderAuthFailureClassifier authFailureClassifier,
        GlossaryModel? glossary = null)
    {
        this.session = session;
        this.llmProvider = new Lazy<ILLMProvider>(llmProviderFactory);
        this.from = from;
        this.to = to;
        this.authFailureClassifier = authFailureClassifier;
        this.glossary = glossary;

        schema = new(() =>
        {
            return project.GetSchema(schemaName)
                ?? throw new InvalidOperationException($"Schema '{schemaName}' not found in project.");
        });
    }

    public TranslationJob(
        IProjectDataSession session,
        ILLMProvider llmProvider,
        Project project,
        string schemaName,
        Language from,
        Language to,
        IProviderAuthFailureClassifier authFailureClassifier,
        GlossaryModel? glossary = null)
        : this(
            session,
            () => llmProvider,
            project,
            schemaName,
            from,
            to,
            authFailureClassifier,
            glossary)
    {
    }

    public bool DryRun { get; set; }

    public Action<string>? Output { get; set; }

    public string SchemaName => schema.Value.Model.Name;

    public void Execute()
    {
        source = [.. session.Get($"{SchemaName}.*:{from}")];
        EnsureSourceContainsRequiredFields();

        LLMChatMessage[] messages = [
            GetSystemPrompt(),
            GetTranslatePrompt()
        ];

        LLMChatMessage result = CompleteWithSafeProviderDiagnostics(messages);
        Dictionary<string, string> translatedFields = GetTranslationMap(result.Content);
        ValidateTranslatedFields(translatedFields);

        int translatedCount = 0;

        foreach (DataRetrieveResult drr in source)
        {
            if (drr.DataModel is null)
            {
                throw new InvalidOperationException(
                    $"Data model for record {drr.RecordLocator} in the source language is null."
                );
            }

            string fieldName = drr.RecordLocator.Field;
            string value = translatedFields[fieldName];

            RecordLocator target = drr.RecordLocator.InLanguage(to);
            bool exists = session.Get(target.AsFilter()).Any(resultItem => resultItem.DataModel is not null);

            if (exists) continue;

            DataModel newData = new()
            {
                Name = fieldName,
                Value = value,
                Meta = new DataMetaModel()
                {
                    Status = DataStatus.NeedsReview
                }
            };

            newData.Meta.UpdateSourceHash(from, fieldName, drr.DataModel.Value);
            newData.Meta.Tick("$ai");

            if (!DryRun)
            {
                session.Set(target, newData);
            }

            WriteLine($"Translated field {fieldName}: {Util.LimitString(value, maxLength: 40)}");
            translatedCount++;
        }

        if (translatedCount == 0)
        {
            WriteLine("No changes.");
        }
        else if (!DryRun)
        {
            try
            {
                session.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new PersistenceException(
                    $"Failed to persist translated records for schema '{SchemaName}' ({from.Code} -> {to.Code}).",
                    ex
                );
            }

            WriteLine("Saving changes... ok");
        }
    }

    public bool HasUntranslatedFields()
    {
        List<DataRetrieveResult> target = [..
            session.Get($"{SchemaName}.*:{to}")
                .Where(drr => drr.DataModel is not null)
        ];

        if (target.Count == 0) return true;

        HashSet<string> translatedFieldNames = [.. target.Select(drr => drr.DataModel!.Name)];

        return schema.Value.Model.Model.Any(x => !translatedFieldNames.Contains(x.Name));
    }

    private Dictionary<string, string> GetTranslationMap(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new TranslationResponseException(
                $"Provider returned an empty translation response for schema '{SchemaName}' ({from.Code} -> {to.Code}).",
                new JsonException("The translation payload was empty.")
            );
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? throw new JsonException("The translation payload was empty.");
        }
        catch (JsonException ex)
        {
            string message = string.Join(
                Environment.NewLine,
                $"The translated JSON is invalid for schema '{SchemaName}' ({from.Code} -> {to.Code}).",
                $"JSON error: {ex.Message}",
                $"Payload excerpt: {GetPayloadExcerpt(json)}"
            );

            throw new TranslationResponseException(message, ex);
        }
    }

    private LLMChatMessage CompleteWithSafeProviderDiagnostics(LLMChatMessage[] messages)
    {
        try
        {
            return llmProvider.Value.Complete(messages);
        }
        catch (CommandExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            string message =
                $"Translation provider failed for schema '{SchemaName}' ({from.Code} -> {to.Code}): {ex.Message}";

            if (authFailureClassifier.IsAuthenticationFailure(ex))
            {
                throw new ProviderAuthenticationException(message, ex);
            }

            throw new ProviderExecutionException(message, ex);
        }
    }

    private void EnsureSourceContainsRequiredFields()
    {
        if (source is null)
        {
            throw new InvalidOperationException("Source data is not loaded.");
        }

        HashSet<string> sourceFields = [
            .. source
                .Where(x => x.DataModel is not null)
                .Select(x => x.DataModel!.Name)
        ];

        List<string> missingFields = [
            .. schema.Value.Model.Model
                .Select(x => x.Name)
                .Where(fieldName => !sourceFields.Contains(fieldName))
        ];

        if (missingFields.Count == 0)
        {
            return;
        }

        throw new MissingSourceDataException(
            $"Source data for schema '{SchemaName}' in language '{from.Code}' is incomplete. Missing fields: {FormatFieldList(missingFields)}."
        );
    }

    private void ValidateTranslatedFields(Dictionary<string, string> translatedFields)
    {
        List<string> expectedFields = [.. schema.Value.Model.Model.Select(x => x.Name)];

        List<string> missingFields = [
            .. expectedFields.Where(fieldName => !translatedFields.ContainsKey(fieldName))
        ];

        if (missingFields.Count > 0)
        {
            throw new TranslationResponseException(
                $"Translated JSON is missing required fields for schema '{SchemaName}' ({from.Code} -> {to.Code}): {FormatFieldList(missingFields)}.",
                new JsonException("The translation payload is missing required fields.")
            );
        }

        List<string> emptyFields = [
            .. expectedFields.Where(fieldName => string.IsNullOrWhiteSpace(translatedFields[fieldName]))
        ];

        if (emptyFields.Count > 0)
        {
            throw new TranslationResponseException(
                $"Translated JSON contains empty values for schema '{SchemaName}' ({from.Code} -> {to.Code}): {FormatFieldList(emptyFields)}.",
                new JsonException("The translation payload contains empty values.")
            );
        }
    }

    private static string GetPayloadExcerpt(string payload)
    {
        string normalized = payload.Trim()
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        if (normalized.Length <= MaxDiagnosticExcerptLength)
        {
            return normalized;
        }

        return $"{normalized[..MaxDiagnosticExcerptLength]}...";
    }

    private static string FormatFieldList(IReadOnlyList<string> fieldNames)
    {
        List<string> fields = [.. fieldNames.Take(MaxFieldListInDiagnostics)];

        if (fieldNames.Count > MaxFieldListInDiagnostics)
        {
            fields.Add($"(+{fieldNames.Count - MaxFieldListInDiagnostics} more)");
        }

        return string.Join(", ", fields);
    }

    private LLMChatMessage GetSystemPrompt()
    {
        return new LLMChatMessage(
            Role: LLMChatMessageRole.System,
            Content: Util.SystemPromptTemplate.Format(GetVariables())
        );
    }

    private LLMChatMessage GetTranslatePrompt()
    {
        return new LLMChatMessage(
            Role: LLMChatMessageRole.User,
            Content: Util.TranslatePromptTemplate.Format(GetVariables())
        );
    }

    private Dictionary<string, string> GetVariables()
    {
        if (variables is not null) return variables;

        ProjectModel project = session.Project.Model;
        Style style = project.Style;

        return variables = new Dictionary<string, string>
        {
            { "source_language", from.GetName() },
            { "target_language", to.GetName() },
            { "project_master_language", project.MasterLanguage.GetName() },
            { "project_name", project.Name },
            { "project_description", project.Description?.Trim() ?? string.Empty },
            { "tone", style.Tone.ToString() },
            { "formality", style.Formality.ToString() },
            { "audience", style.Audience },
            { "section_name", SchemaName },
            { "section_description", schema.Value.Model.Description?.Trim() ?? string.Empty },
            { "fields_list", GetFieldsList() },
            { "source_json", GetSourceJson() },
            { "glossary_rules", GetGlossaryRules() },
            { "glossary_source_hint", GetGlossarySourceHint() },
        };
    }

    private string GetGlossarySourceHint()
    {
        Language masterLanguage = session.Project.Model.MasterLanguage;

        if (from.Equals(masterLanguage))
        {
            return "Source language matches the project master language. Apply glossary rules normally.";
        }

        return $"Source language is {from.GetLongName()}, but glossary terms are keyed to the project master language {masterLanguage.GetLongName()}. Treat glossary matches as soft hints and prioritize semantic correctness.";
    }

    private string GetGlossaryRules()
    {
        if (glossary is null)
        {
            return "No glossary file was found for the target language.";
        }

        GlossaryEntryModel[] aiEntries = [.. glossary.Entries.Where(AppliesToAi)];

        if (aiEntries.Length == 0)
        {
            return "A glossary file exists, but no entries apply to AI translation guidance.";
        }

        StringBuilder sb = new();

        sb.Append("Glossary language: ");
        sb.AppendLine(glossary.Language.GetLongName());
        sb.AppendLine("Entries:");

        foreach (GlossaryEntryModel entry in aiEntries)
        {
            sb.Append("- term: ");
            sb.AppendLine(entry.Term);
            sb.Append("  translation: ");
            sb.AppendLine(entry.Translation);
            sb.Append("  status: ");
            sb.AppendLine(FormatGlossaryStatus(entry.Status));

            if (!string.IsNullOrWhiteSpace(entry.Note))
            {
                sb.Append("  note: ");
                sb.AppendLine(entry.Note.Trim());
            }

            if (entry.ForbiddenVariants.Length > 0)
            {
                sb.AppendLine("  forbidden_variants:");
                foreach (string forbiddenVariant in entry.ForbiddenVariants)
                {
                    sb.Append("    - ");
                    sb.AppendLine(forbiddenVariant);
                }
            }

            sb.Append("  applies_to: ");
            sb.AppendLine(FormatGlossaryContexts(entry));
        }

        return sb.ToString().TrimEnd();
    }

    private static bool AppliesToAi(GlossaryEntryModel entry)
    {
        return entry.AppliesTo.Contains(GlossaryAppliesTo.Ai);
    }

    private static string FormatGlossaryStatus(GlossaryEntryStatus status)
    {
        return status.ToString().ToLowerInvariant();
    }

    private static string FormatGlossaryContexts(GlossaryEntryModel entry)
    {
        return string.Join(", ",
            entry.AppliesTo
                .Select(x => x.ToString().ToLowerInvariant())
                .Distinct()
        );
    }

    private string GetFieldsList()
    {
        StringBuilder sb = new();

        foreach (SchemaFieldModel field in schema.Value.Model.Model)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append("- ");
            sb.Append(field.Name);

            if (!string.IsNullOrWhiteSpace(field.Description))
            {
                sb.Append(": ");
                sb.Append(field.Description);
            }
        }

        return sb.ToString();
    }

    private string GetSourceJson()
    {
        if (source is null) throw new InvalidOperationException("Source data is not loaded.");

        JsonObject jsonObject = [];

        foreach (DataModel? dataModel in source.Select(x => x.DataModel))
        {
            if (dataModel is null) continue;
            jsonObject[dataModel.Name] = dataModel.Value;
        }

        return jsonObject.ToJsonString(JsonOptions);
    }

    private void WriteLine(string message)
    {
        if (Output is null)
        {
            Console.WriteLine(message);
            return;
        }

        Output(message);
    }
}
