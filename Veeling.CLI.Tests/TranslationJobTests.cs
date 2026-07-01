using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class TranslationJobTests
{
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
}
