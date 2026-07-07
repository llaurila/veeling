using Veeling.CLI;
using Veeling.CLI.Providers;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class TranslateApplicationServiceTests
{
    [Fact]
    public void BuildWorkPlan_NoOpTargets_ReturnsZeroCandidates()
    {
        DirectoryInfo projectDirectory = CreateSandbox();

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new DataModel { Name = "Field1", Value = "Hei" });

            Project project = new(projectFile);

            TranslateApplicationService service = new(new TranslationJobFactory(
                new FileSystemProjectDataSessionFactory(),
                new StaticJsonProviderFactory("{\"Field1\":\"Hei\"}"),
                new GlossaryLoader(),
                new ProviderAuthFailureClassifier()));

            TranslateWorkPlan plan = service.BuildWorkPlan(project, new Language("en"), [new Language("fi")], changed: false);

            Assert.Equal(0, plan.TotalCandidateFields);
            Assert.Single(plan.SchemaPlans);
            Assert.Empty(plan.SchemaPlans[0].CandidateFields);
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
    public void BuildWorkPlan_MultiSchemaMultiTarget_AggregatesCandidateTotals()
    {
        DirectoryInfo projectDirectory = CreateSandbox();

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi", "fr"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema2", "Field1", "Field2");

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "S1" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "en",
                new DataModel { Name = "Field1", Value = "S2F1" },
                new DataModel { Name = "Field2", Value = "S2F2" });

            Project project = new(projectFile);

            TranslateApplicationService service = new(new TranslationJobFactory(
                new FileSystemProjectDataSessionFactory(),
                new StaticJsonProviderFactory("{\"Field1\":\"X\",\"Field2\":\"Y\"}"),
                new GlossaryLoader(),
                new ProviderAuthFailureClassifier()));

            TranslateWorkPlan plan = service.BuildWorkPlan(project, new Language("en"), [new Language("fi"), new Language("fr")], changed: false);

            Assert.Equal(6, plan.TotalCandidateFields);
            Assert.Equal(4, plan.SchemaPlans.Count);
            Assert.All(plan.SchemaPlans, schemaPlan => Assert.NotEmpty(schemaPlan.CandidateFields));
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
    public void Execute_EmitsWarningAndStableLineOrder_ForMasterAndSkippedSchema()
    {
        DirectoryInfo projectDirectory = CreateSandbox();

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi", "fr"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.EnsureDataDirectory(projectDirectory);

            File.WriteAllText(
                Path.Combine(projectDirectory.FullName, Project.DataDirectoryName, "Schema1.fi.yaml"),
                "- name: Field1\n  value: Tervetuloa\n"
            );

            File.WriteAllText(
                Path.Combine(projectDirectory.FullName, Project.DataDirectoryName, "Schema1.fr.yaml"),
                "- name: Field1\n  value: Bienvenue\n"
            );

            Project project = new(projectFile);

            TranslateApplicationService service = new(new TranslationJobFactory(
                new FileSystemProjectDataSessionFactory(),
                new StaticJsonProviderFactory("{\"Field1\":\"Bonjour\"}"),
                new GlossaryLoader(),
                new ProviderAuthFailureClassifier()));

            TranslateCommandResult result = service.Execute(
                project,
                new Language("fi"),
                [new Language("fr")],
                dryRun: true,
                changed: false);

            Assert.NotNull(result.Warning);
            Assert.Contains("translation quality may suffer", result.Warning!, StringComparison.OrdinalIgnoreCase);

            Assert.Collection(result.OutputLines,
                line => Assert.Equal("Processing schema 'Schema1' ('fi' -> 'fr')...", line),
                line => Assert.Equal("All fields are already translated, skipping.", line));
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
    public void Execute_WithProgressSink_EmitsSchemaFieldAndSaveEvents()
    {
        DirectoryInfo projectDirectory = CreateSandbox();

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });

            Project project = new(projectFile);

            TranslateApplicationService service = new(new TranslationJobFactory(
                new FileSystemProjectDataSessionFactory(),
                new StaticJsonProviderFactory("{\"Field1\":\"Hei\"}"),
                new GlossaryLoader(),
                new ProviderAuthFailureClassifier()));

            List<TranslateProgressEvent> events = [];

            TranslateCommandResult result = service.Execute(
                project,
                new Language("en"),
                [new Language("fi")],
                dryRun: false,
                changed: false,
                onProgress: events.Add);

            Assert.Null(result.Warning);
            Assert.Collection(events,
                e =>
                {
                    Assert.Equal(TranslateProgressEventKind.SchemaStarted, e.Kind);
                    Assert.Equal("Schema1", e.SchemaName);
                    Assert.Equal(1, e.TotalCount);
                },
                e =>
                {
                    Assert.Equal(TranslateProgressEventKind.FieldTranslated, e.Kind);
                    Assert.Equal("Field1", e.FieldName);
                    Assert.Equal(1, e.CompletedCount);
                    Assert.Equal(1, e.TotalCount);
                    Assert.Equal(1, e.SchemaCompletedCount);
                    Assert.Equal(1, e.SchemaTotalCount);
                    Assert.False(e.DryRun);
                },
                e => Assert.Equal(TranslateProgressEventKind.SaveStarted, e.Kind),
                e => Assert.Equal(TranslateProgressEventKind.SaveCompleted, e.Kind));
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
    public void Execute_WithProgressSink_EmitsSchemaSkippedForZeroCandidates()
    {
        DirectoryInfo projectDirectory = CreateSandbox();

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new DataModel { Name = "Field1", Value = "Hei" });

            Project project = new(projectFile);

            TranslateApplicationService service = new(new TranslationJobFactory(
                new FileSystemProjectDataSessionFactory(),
                new StaticJsonProviderFactory("{\"Field1\":\"Hei\"}"),
                new GlossaryLoader(),
                new ProviderAuthFailureClassifier()));

            List<TranslateProgressEvent> events = [];

            service.Execute(
                project,
                new Language("en"),
                [new Language("fi")],
                dryRun: true,
                changed: false,
                onProgress: events.Add);

            Assert.Collection(events,
                e => Assert.Equal(TranslateProgressEventKind.SchemaStarted, e.Kind),
                e =>
                {
                    Assert.Equal(TranslateProgressEventKind.SchemaSkipped, e.Kind);
                    Assert.Equal(0, e.SchemaTotalCount);
                    Assert.Equal(0, e.TotalCount);
                });
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    private static DirectoryInfo CreateSandbox()
    {
        string path = Path.Combine(Path.GetTempPath(), "Veeling.TranslateAppServiceTests", Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path);
    }

    private sealed class StaticJsonProviderFactory(string json) : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            return new StaticJsonProvider(json);
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
