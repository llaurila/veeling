using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class TranslationJobChangedSelectionTests
{
    [Fact]
    public void HasUntranslatedFields_DefaultMode_IgnoresChangedExistingTargets()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = CreateSessionWithDriftedTarget(project);

        TranslationJob job = CreateJob(session, project);

        Assert.False(job.HasUntranslatedFields());
    }

    [Fact]
    public void HasUntranslatedFields_ChangedMode_DetectsDriftedTarget()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = CreateSessionWithDriftedTarget(project);

        TranslationJob job = CreateJob(session, project);
        job.IncludeChanged = true;

        Assert.True(job.HasUntranslatedFields());
    }

    [Fact]
    public void HasUntranslatedFields_ChangedMode_UpToDateTargets_ReturnsFalse()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = CreateSessionWithUpToDateTarget(project);

        TranslationJob job = CreateJob(session, project);
        job.IncludeChanged = true;

        Assert.False(job.HasUntranslatedFields());
    }

    private static TranslationJob CreateJob(MockProjectDataSession session, Project project)
    {
        return new TranslationJob(
            session,
            new StaticJsonProvider(),
            project,
            "Schema1",
            new Language("en"),
            new Language("fi"),
            new ProviderAuthFailureClassifier())
        {
            DryRun = true
        };
    }

    private static MockProjectDataSession CreateSessionWithDriftedTarget(Project project)
    {
        DataModel source = new()
        {
            Name = "Field1",
            Value = "Hello new"
        };

        DataMetaModel targetMeta = new()
        {
            Status = DataStatus.Approved
        };
        targetMeta.UpdateSourceHash(new Language("en"), "Field1", "Hello old");

        DataModel target = new()
        {
            Name = "Field1",
            Value = "Hei",
            Meta = targetMeta
        };

        return new MockProjectDataSession(project)
        {
            OnGet = recordFilter => recordFilter.ToString() switch
            {
                "Schema1.*:en" =>
                [
                    new DataRetrieveResult(source, new RecordLocator("Schema1", "Field1", "en"))
                ],
                "Schema1.*:fi" =>
                [
                    new DataRetrieveResult(target, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(target, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                _ => []
            }
        };
    }

    private static MockProjectDataSession CreateSessionWithUpToDateTarget(Project project)
    {
        DataModel source = new()
        {
            Name = "Field1",
            Value = "Hello new"
        };

        DataMetaModel targetMeta = new()
        {
            Status = DataStatus.Approved
        };
        targetMeta.UpdateSourceHash(new Language("en"), "Field1", "Hello new");

        DataModel target = new()
        {
            Name = "Field1",
            Value = "Hei",
            Meta = targetMeta
        };

        return new MockProjectDataSession(project)
        {
            OnGet = recordFilter => recordFilter.ToString() switch
            {
                "Schema1.*:en" =>
                [
                    new DataRetrieveResult(source, new RecordLocator("Schema1", "Field1", "en"))
                ],
                "Schema1.*:fi" =>
                [
                    new DataRetrieveResult(target, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(target, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                _ => []
            }
        };
    }

    private sealed class StaticJsonProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, "{\"Field1\":\"Hei\"}");
        }
    }
}
