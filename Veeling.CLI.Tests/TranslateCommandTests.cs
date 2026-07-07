using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class TranslateCommandTests
{
    [Fact]
    public async Task Translate_OutputsLegacyBufferedLinesInStableOrder()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new StaticJsonProviderFactory("{\"Field1\":\"Hei\"}"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi",
                "--dry-run"
            ]);

            Assert.Equal(0, code);

            string[] lines = console.StdOut.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Processing schema 'Schema1' ('en' -> 'fi')...", lines[0]);
            Assert.Equal("[1/1] Translated field Schema1.Field1: Hei", lines[1]);
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
    public async Task Translate_WithNonMasterSource_WarnsAboutQualityAndSoftHints()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

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

            using ConsoleCapture console = new();

            int code = await app.RunAsync(
            [
                "translate",
                "--project-file", projectFile.FullName,
                "--from", "fi",
                "--to", "fr",
                "--dry-run"
            ]);

            Assert.Equal(0, code);
            Assert.Contains(
                "translation quality may suffer",
                console.StdErr.ToString(),
                StringComparison.OrdinalIgnoreCase
            );
            Assert.Contains(
                "soft hints",
                console.StdErr.ToString(),
                StringComparison.OrdinalIgnoreCase
            );

            string[] lines = console.StdOut.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Processing schema 'Schema1' ('fi' -> 'fr')...", lines[0]);
            Assert.Equal("All fields are already translated, skipping.", lines[1]);
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
    public async Task Translate_InvalidJsonPayload_ReturnsParseFailureExitCode()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new InvalidJsonProviderFactory());
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi"
            ]);

            Assert.Equal(2, code);
            Assert.Contains("translated json is invalid", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Translate_ProviderFailure_ReturnsProviderFailureExitCode()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FailingProviderFactory("provider timeout"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi"
            ]);

            Assert.Equal(3, code);
            Assert.Contains("translation provider failed", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Translate_ProviderAuthenticationFailure_ReturnsAuthFailureExitCode()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FailingProviderFactory("Unauthorized: invalid api key"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi"
            ]);

            Assert.Equal(4, code);
            Assert.Contains("translation provider failed", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Translate_ProviderInitializationAuthenticationFailure_ReturnsAuthFailureExitCode()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FailingInitProviderFactory("Unauthorized: invalid api key"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi"
            ]);

            Assert.Equal(4, code);
            Assert.Contains("failed to initialize translation provider", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Translate_ProviderInitializationFailure_ReturnsProviderFailureExitCode()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FailingInitProviderFactory("Provider bootstrap timeout"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi"
            ]);

            Assert.Equal(3, code);
            Assert.Contains("failed to initialize translation provider", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Translate_MissingSourceDataFile_ReturnsMissingSourceExitCode()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.EnsureDataDirectory(projectDirectory);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi"
            ]);

            Assert.Equal(5, code);
            Assert.Contains("does not exist", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Translate_StreamedProgress_DoesNotDuplicateOutputLines()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new StaticJsonProviderFactory("{\"Field1\":\"Hei\"}"));
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.TranslateTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "translate",
                "--project-file", projectFile.FullName,
                "--to", "fi",
                "--dry-run"
            ]);

            Assert.Equal(0, code);

            string[] lines = console.StdOut.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(2, lines.Length);
            Assert.Equal("Processing schema 'Schema1' ('en' -> 'fi')...", lines[0]);
            Assert.Equal("[1/1] Translated field Schema1.Field1: Hei", lines[1]);
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

    private sealed class InvalidJsonProviderFactory : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            return new InvalidJsonProvider();
        }
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

    private sealed class InvalidJsonProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new LLMChatMessage(LLMChatMessageRole.Assistant, "{not-json");
        }
    }

    private sealed class FailingProviderFactory(string message) : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            return new FailingProvider(message);
        }
    }

    private sealed class FailingInitProviderFactory(string message) : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FailingProvider(string message) : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            throw new InvalidOperationException(message);
        }
    }
}
