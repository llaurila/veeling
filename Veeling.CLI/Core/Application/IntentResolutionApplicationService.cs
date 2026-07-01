using Veeling.CLI;
using Veeling.CLI.Commands;
using Veeling.CLI.Providers;

namespace Veeling.Core.Application;

public sealed record IntentResolutionRequest(string Intent, FileInfo ProjectFile);

public sealed class IntentResolutionApplicationService(
    ICommandCatalogBuilder commandCatalogBuilder,
    IIntentParserProvider intentParserProvider,
    IGlobalConfigFileLocator globalConfigFileLocator)
{
    private const string DefaultProvider = "openai";

    public IntentResolutionResult Resolve(IntentResolutionRequest request)
    {
        CommandCatalog catalog = commandCatalogBuilder.Build();
        IntentParserProjectContext context = BuildProjectContext(request.ProjectFile);
        IntentParserProviderSelection selection = ResolveProviderSelection(request.ProjectFile);

        IntentParserResponse parserResponse;
        try
        {
            parserResponse = intentParserProvider.Parse(selection, new IntentParserRequest(
                Intent: request.Intent,
                Catalog: catalog,
                ProjectContext: context));
        }
        catch (Exception ex)
        {
            return new IntentResolutionResult(
                Outcome: IntentResolutionOutcome.Unsupported,
                Command: null,
                Message: $"Intent parser failed: {ex.Message}");
        }

        return ValidateAndShape(parserResponse, catalog);
    }

    private static IntentResolutionResult ValidateAndShape(IntentParserResponse response, CommandCatalog catalog)
    {
        string outcome = (response.Outcome ?? string.Empty).Trim().ToLowerInvariant();

        if (outcome is "clarification")
        {
            return new IntentResolutionResult(
                IntentResolutionOutcome.Clarification,
                null,
                response.Message ?? "Please clarify your intent.");
        }

        if (outcome is "unsupported")
        {
            return new IntentResolutionResult(
                IntentResolutionOutcome.Unsupported,
                null,
                response.Message ?? "Intent is unsupported.");
        }

        if (outcome is not "resolved")
        {
            return new IntentResolutionResult(
                IntentResolutionOutcome.Unsupported,
                null,
                "Intent parser returned an unknown outcome.");
        }

        if (response.Command is null)
        {
            return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, "Resolved outcome must include exactly one command.");
        }

        if (response.Commands is { Count: > 0 })
        {
            return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, "Only single-command resolution is supported.");
        }

        IntentParserCommandSpec commandSpec = response.Command;

        if (commandSpec.Path.Count == 0)
        {
            return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, "Resolved command path is missing.");
        }

        if (commandSpec.Path.Any(segment => string.Equals(segment, "ai", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "ask", StringComparison.OrdinalIgnoreCase)))
        {
            return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, "Recursive intent resolution to ai/ask is not allowed.");
        }

        CommandCatalogEntry? commandEntry = catalog.FindByPath(commandSpec.Path);
        if (commandEntry is null)
        {
            return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, "Resolved command is not part of Veeling command surface.");
        }

        Dictionary<string, string?> normalizedOptions = [];
        if (commandSpec.Options is not null)
        {
            foreach ((string rawKey, string? value) in commandSpec.Options)
            {
                string key = rawKey.Trim();

                CommandCatalogOption? matchedOption = commandEntry.Options.FirstOrDefault(option =>
                    string.Equals(option.Name, key, StringComparison.OrdinalIgnoreCase)
                    || option.Aliases.Any(alias => string.Equals(alias, key, StringComparison.OrdinalIgnoreCase)));

                if (matchedOption is null)
                {
                    return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, $"Unknown option '{rawKey}' for command '{string.Join(' ', commandEntry.PathSegments)}'.");
                }

                if (!matchedOption.RequiresValue)
                {
                    normalizedOptions[matchedOption.Name] = null;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return new IntentResolutionResult(IntentResolutionOutcome.Unsupported, null, $"Option '{matchedOption.Name}' requires a value.");
                    }

                    normalizedOptions[matchedOption.Name] = value;
                }
            }
        }

        List<string> args = commandSpec.Arguments is null ? [] : [.. commandSpec.Arguments];
        int minArity = commandEntry.Arguments.Sum(arg => arg.MinimumArity);
        int maxArity = commandEntry.Arguments.Sum(arg => arg.MaximumArity < 0 ? int.MaxValue : arg.MaximumArity);

        if (args.Count < minArity || args.Count > maxArity)
        {
            return new IntentResolutionResult(
                IntentResolutionOutcome.Unsupported,
                null,
                "Resolved command arguments do not satisfy command arity constraints.");
        }

        bool suggestionOnlyByPolicy = commandEntry.PathSegments.Count > 0
            && (string.Equals(commandEntry.PathSegments[0], "onboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandEntry.PathSegments[0], "update", StringComparison.OrdinalIgnoreCase));

        bool suggestionOnly = suggestionOnlyByPolicy || commandSpec.SuggestionOnly == true;

        string canonicalInvocation = BuildCanonicalInvocation(commandEntry.PathSegments, normalizedOptions, args);
        ResolvedCommand resolved = new(
            PathSegments: commandEntry.PathSegments,
            Options: normalizedOptions,
            Arguments: args,
            Explanation: response.Explanation ?? response.Message ?? "Resolved from natural-language intent.",
            RequiresConfirmation: true,
            SupportsProjectFileOption: commandEntry.Options.Any(option => string.Equals(option.Name, "--project-file", StringComparison.OrdinalIgnoreCase)),
            IsSuggestionOnly: suggestionOnly,
            SuggestionReason: suggestionOnly
                ? (commandSpec.SuggestionReason ?? "This command is suggestion-only in v1 natural-language flow.")
                : null,
            CanonicalInvocation: canonicalInvocation);

        return new IntentResolutionResult(IntentResolutionOutcome.Resolved, resolved, "Intent resolved.");
    }

    private IntentParserProviderSelection ResolveProviderSelection(FileInfo projectFile)
    {
        VeelingConfig config = new(projectFile.Directory ?? new DirectoryInfo(Directory.GetCurrentDirectory()), globalConfigFileLocator);

        string provider = (config.GetValue("intent_parser_provider")
            ?? config.GetValue("llm_provider")
            ?? DefaultProvider).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = DefaultProvider;
        }

        string? model = config.GetValue("intent_parser_model");
        if (string.IsNullOrWhiteSpace(model))
        {
            model = null;
        }

        DirectoryInfo configDirectory = projectFile.Directory ?? new DirectoryInfo(Directory.GetCurrentDirectory());

        return new IntentParserProviderSelection(provider, model, configDirectory);
    }

    private static IntentParserProjectContext BuildProjectContext(FileInfo projectFile)
    {
        try
        {
            if (!projectFile.Exists)
            {
                return new IntentParserProjectContext(false, null, []);
            }

            Project project = new(projectFile);
            return new IntentParserProjectContext(
                ProjectDetected: true,
                MasterLanguage: project.Model.MasterLanguage.Code,
                Languages: [.. project.Model.Languages.Select(language => language.Code)]);
        }
        catch
        {
            return new IntentParserProjectContext(false, null, []);
        }
    }

    private static string BuildCanonicalInvocation(
        IReadOnlyList<string> path,
        IReadOnlyDictionary<string, string?> options,
        IReadOnlyList<string> args)
    {
        List<string> tokens = ["veeling", .. path];

        foreach ((string key, string? value) in options.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            if (value is null)
            {
                tokens.Add(key);
            }
            else
            {
                tokens.Add(key);
                tokens.Add(value);
            }
        }

        tokens.AddRange(args);

        return string.Join(' ', tokens.Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string token)
    {
        return token.Any(char.IsWhiteSpace)
            ? $"\"{token.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : token;
    }
}
