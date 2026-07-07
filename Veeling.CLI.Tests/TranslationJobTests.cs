using Veeling.CLI.Providers;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class TranslationJobTests
{
    [Fact]
    public void Execute_OneField_ReportsFieldLineInDeterministicOrder()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter => recordFilter.ToString() switch
            {
                "Schema1.*:en" =>
                [
                    new DataRetrieveResult(new DataModel { Name = "Field1", Value = "Hello" }, new RecordLocator("Schema1", "Field1", "en"))
                ],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(null, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                "Schema1.*:fi" => [],
                _ => []
            }
        };

        TranslationJob job = new(
            session,
            new StaticJsonProvider("{\"Field1\":\"Hei\"}"),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier())
        {
            DryRun = true
        };

        List<string> output = [];
        job.Output = output.Add;

        job.Execute();

        Assert.Collection(output,
            line => Assert.Equal("Translated field Field1: Hei", line));
    }

    [Fact]
    public void Execute_DryRun_ReportsTranslatedFieldsWithoutSaving()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter => recordFilter.ToString() switch
            {
                "Schema1.*:en" =>
                [
                    new DataRetrieveResult(new DataModel { Name = "Field1", Value = "Hello" }, new RecordLocator("Schema1", "Field1", "en"))
                ],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(null, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                "Schema1.*:fi" => [],
                _ => []
            }
        };

        TranslationJob job = new(
            session,
            new StaticJsonProvider("{\"Field1\":\"Hei\"}"),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier())
        {
            DryRun = true
        };

        List<string> output = [];
        job.Output = output.Add;

        job.Execute();

        Assert.Contains("Translated field Field1: Hei", output, StringComparer.Ordinal);
        Assert.DoesNotContain("Saving changes... ok", output, StringComparer.Ordinal);
        Assert.Equal(0, session.SaveChangesCalls);
    }

    [Fact]
    public void Execute_NonDryRun_ReportsSaveMessage()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter => recordFilter.ToString() switch
            {
                "Schema1.*:en" =>
                [
                    new DataRetrieveResult(new DataModel { Name = "Field1", Value = "Hello" }, new RecordLocator("Schema1", "Field1", "en"))
                ],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(null, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                "Schema1.*:fi" => [],
                _ => []
            }
        };

        TranslationJob job = new(
            session,
            new StaticJsonProvider("{\"Field1\":\"Hei\"}"),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier())
        {
            DryRun = false
        };

        List<string> output = [];
        job.Output = output.Add;

        job.Execute();

        Assert.Contains("Saving changes... ok", output, StringComparer.Ordinal);
        Assert.Equal(1, session.SaveChangesCalls);
    }

    [Fact]
    public void Execute_EmitsProgressEvents_ForFieldAndSaveLifecycle()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter => recordFilter.ToString() switch
            {
                "Schema1.*:en" =>
                [
                    new DataRetrieveResult(new DataModel { Name = "Field1", Value = "Hello" }, new RecordLocator("Schema1", "Field1", "en"))
                ],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(null, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                "Schema1.*:fi" => [],
                _ => []
            }
        };

        TranslationJob job = new(
            session,
            new StaticJsonProvider("{\"Field1\":\"Hei\"}"),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier())
        {
            DryRun = false
        };

        job.ConfigureProgressCounters(
            progressCompletedCount: 0,
            progressTotalCount: 1,
            schemaProgressCompletedCount: 0,
            schemaProgressTotalCount: 1);

        List<TranslateProgressEvent> events = [];
        job.OnProgress = events.Add;

        job.Execute();

        Assert.Collection(events,
            e => Assert.Equal(TranslateProgressEventKind.FieldTranslated, e.Kind),
            e => Assert.Equal(TranslateProgressEventKind.SaveStarted, e.Kind),
            e => Assert.Equal(TranslateProgressEventKind.SaveCompleted, e.Kind));
    }

    [Fact]
    public void Execute_IncludesGlossaryRulesAndSoftHintInPrompt()
    {
        Project project = MockData.GetMockProject("foobar");
        CapturingLLMProvider llmProvider = new();

        MockProjectDataSession session = new(project)
        {
            OnGet = recordFilter =>
            {
                if (recordFilter.ToString() == "Schema1.*:en")
                {
                    return
                    [
                        new DataRetrieveResult(
                            new DataModel { Name = "Field1", Value = "Sign in" },
                            new RecordLocator("Schema1", "Field1", "en")
                        )
                    ];
                }

                if (recordFilter.ToString() == "Schema1.Field1:fr")
                {
                    return
                    [
                        new DataRetrieveResult(
                            null,
                            new RecordLocator("Schema1", "Field1", "fr")
                        )
                    ];
                }

                return [];
            }
        };

        GlossaryModel glossary = new()
        {
            Language = new Language("fr"),
            Entries =
            [
                new GlossaryEntryModel
                {
                    Term = "Sign in",
                    Translation = "Se connecter",
                    Status = GlossaryEntryStatus.Approved,
                    AppliesTo = [GlossaryAppliesTo.Ai],
                    ForbiddenVariants = ["Connexion"],
                    Note = "Use as button text"
                },
                new GlossaryEntryModel
                {
                    Term = "Backend",
                    Translation = "Serveur",
                    Status = GlossaryEntryStatus.Deprecated,
                    AppliesTo = [GlossaryAppliesTo.Ui],
                    ForbiddenVariants = []
                }
            ]
        };

        TranslationJob job = new(
            session,
            llmProvider,
            project,
            "Schema1",
            new Language("en"),
            new Language("fr"),
            new ProviderAuthFailureClassifier(),
            glossary)
        {
            DryRun = true
        };

        job.Execute();

        Assert.NotNull(llmProvider.LastHistory);
        Assert.Equal(2, llmProvider.LastHistory!.Length);

        string systemPrompt = llmProvider.LastHistory[0].Content;
        string userPrompt = llmProvider.LastHistory[1].Content;

        Assert.Contains("glossary terms are keyed to the project master language", systemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Treat glossary matches as soft hints", systemPrompt, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Sign in", userPrompt, StringComparison.Ordinal);
        Assert.Contains("Se connecter", userPrompt, StringComparison.Ordinal);
        Assert.Contains("Connexion", userPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Backend", userPrompt, StringComparison.Ordinal);
    }

    private sealed class CapturingLLMProvider : ILLMProvider
    {
        public LLMChatMessage[]? LastHistory { get; private set; }

        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            LastHistory = history;
            return new LLMChatMessage(
                LLMChatMessageRole.Assistant,
                "{\"Field1\":\"Se connecter\"}"
            );
        }
    }

    private sealed class StaticJsonProvider(string json) : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, json);
        }
    }
}
