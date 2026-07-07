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

    [Fact]
    public void GetTranslationCandidateFields_DefaultMode_ReturnsMissingOnly()
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
                "Schema1.*:fi" => [],
                "Schema1.Field1:fi" =>
                [
                    new DataRetrieveResult(null, new RecordLocator("Schema1", "Field1", "fi"))
                ],
                _ => []
            }
        };

        TranslationJob job = CreateJob(session, project);

        IReadOnlyList<string> candidates = job.GetTranslationCandidateFields();

        Assert.Equal(["Field1"], candidates);
    }

    [Fact]
    public void GetTranslationCandidateFields_ChangedMode_ReturnsMissingAndDriftedFields()
    {
        DirectoryInfo projectDirectory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "Veeling.TranslationJobChangedSelectionTests",
            Guid.NewGuid().ToString("N")));

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1", "Field2");
            Project project = new(projectFile);

            DataModel source1 = new() { Name = "Field1", Value = "Hello (new)" };
            DataModel source2 = new() { Name = "Field2", Value = "World" };

            DataMetaModel targetMeta = new() { Status = DataStatus.Approved };
            targetMeta.UpdateSourceHash(new Language("en"), "Field1", "Hello (old)");

            DataModel target1 = new() { Name = "Field1", Value = "Hei vanha", Meta = targetMeta };

            MockProjectDataSession session = new(project)
            {
                OnGet = recordFilter => recordFilter.ToString() switch
                {
                    "Schema1.*:en" =>
                    [
                        new DataRetrieveResult(source1, new RecordLocator("Schema1", "Field1", "en")),
                        new DataRetrieveResult(source2, new RecordLocator("Schema1", "Field2", "en"))
                    ],
                    "Schema1.*:fi" =>
                    [
                        new DataRetrieveResult(target1, new RecordLocator("Schema1", "Field1", "fi"))
                    ],
                    "Schema1.Field1:fi" =>
                    [
                        new DataRetrieveResult(target1, new RecordLocator("Schema1", "Field1", "fi"))
                    ],
                    "Schema1.Field2:fi" =>
                    [
                        new DataRetrieveResult(null, new RecordLocator("Schema1", "Field2", "fi"))
                    ],
                    _ => []
                }
            };

            TranslationJob job = CreateJob(session, project);
            job.IncludeChanged = true;

            IReadOnlyList<string> candidates = job.GetTranslationCandidateFields();

            Assert.Equal(["Field1", "Field2"], candidates);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void GetTranslationCandidateFields_ChangedMode_UpToDateTargets_ReturnsEmpty()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = CreateSessionWithUpToDateTarget(project);

        TranslationJob job = CreateJob(session, project);
        job.IncludeChanged = true;

        IReadOnlyList<string> candidates = job.GetTranslationCandidateFields();

        Assert.Empty(candidates);
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
