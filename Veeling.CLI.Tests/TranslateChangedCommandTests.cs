using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class TranslateChangedCommandTests
{
    [Fact]
    public async Task Translate_WithoutChanged_DoesNotRetranslateExistingDriftedTargets()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateChangedTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

            DataModel source = new()
            {
                Name = "Field1",
                Value = "Hello (new)"
            };

            DataMetaModel targetMeta = new()
            {
                Status = DataStatus.Approved
            };
            targetMeta.UpdateSourceHash(new Language("en"), "Field1", "Hello (old)");

            DataModel target = new()
            {
                Name = "Field1",
                Value = "Hei",
                Meta = targetMeta
            };

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", source);
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", target);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi",
                "--dry-run"
            ]);

            Assert.Equal(0, code);
            Assert.Contains("All fields are already translated, skipping.", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Translated field", console.StdOut.ToString(), StringComparison.Ordinal);
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
    public async Task Translate_WithChanged_IncludesMissingAndDriftedTargets()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new StaticJsonProviderFactory("{\"Field1\":\"Hei uusi\",\"Field2\":\"Maailma\"}"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateChangedTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1", "Field2");

            DataModel source1 = new() { Name = "Field1", Value = "Hello (new)" };
            DataModel source2 = new() { Name = "Field2", Value = "World" };

            DataMetaModel targetMeta = new()
            {
                Status = DataStatus.Approved
            };
            targetMeta.UpdateSourceHash(new Language("en"), "Field1", "Hello (old)");

            DataModel target = new()
            {
                Name = "Field1",
                Value = "Hei vanha",
                Meta = targetMeta
            };

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", source1, source2);
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", target);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi",
                "--changed",
                "--dry-run"
            ]);

            Assert.Equal(0, code);
            string output = console.StdOut.ToString();
            Assert.Contains("Translated field Field1", output, StringComparison.Ordinal);
            Assert.Contains("Translated field Field2", output, StringComparison.Ordinal);
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
    public async Task Translate_WithChangedAndNonMasterFrom_DoesNotApplyChangedDetection()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new StaticJsonProviderFactory("{\"Field1\":\"Bonjour\"}"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateChangedTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi", "fr"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new DataModel
            {
                Name = "Field1",
                Value = "Moi uusi"
            });

            DataMetaModel frMeta = new()
            {
                Status = DataStatus.Approved
            };
            frMeta.UpdateSourceHash(new Language("fi"), "Field1", "Moi vanha");

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fr", new DataModel
            {
                Name = "Field1",
                Value = "Bonjour",
                Meta = frMeta
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--from", "fi",
                "--to", "fr",
                "--changed",
                "--dry-run"
            ]);

            Assert.Equal(0, code);
            Assert.Contains("translation quality may suffer", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("All fields are already translated, skipping.", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Translated field", console.StdOut.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    private static ServiceProvider CreateServiceProvider(ILLMProviderFactory llmProviderFactory)
    {
        ServiceCollection services = new();
        services.AddVeelingCli(new ConfigurationBuilder().Build());
        services.AddSingleton(llmProviderFactory);
        return services.BuildServiceProvider();
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
