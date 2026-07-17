using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class CommandIntegrationTests
{
    [Fact]
    public async Task Init_UsesAllSuppliedOptionsWithoutPrompting()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo rootDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            using CurrentDirectoryScope _ = new(rootDirectory.FullName);
            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "init",
                "--name", "my_project",
                "--description", "Test description",
                "--languages", "en,fi",
                "--master-language", "en",
                "--tone", "neutral",
                "--formality", "formal",
                "--audience", "developers",
                "--yes"
            ]);

            Assert.Equal(0, code);
            Assert.DoesNotContain("Project name:", console.StdOut.ToString(), StringComparison.Ordinal);

            Project project = new(Path.Combine(rootDirectory.FullName, "my_project", Project.ProjectFileName));
            Assert.Equal("developers", project.Model.Style.Audience);
            Assert.Equal(Formality.Formal, project.Model.Style.Formality);
            Assert.Contains(project.Model.Languages, language => language.Code == "fi");
        }
        finally
        {
            if (rootDirectory.Exists)
            {
                rootDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Config_UsesSelectedProjectDirectoryForLocalConfig()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo rootDirectory = Directory.CreateDirectory(rootPath);
        DirectoryInfo projectDirectory = Directory.CreateDirectory(Path.Combine(rootDirectory.FullName, "nested", "project"));
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory);
        string configFilePath = Path.Combine(projectDirectory.FullName, VeelingConfig.ConfigFileName);

        try
        {
            using CurrentDirectoryScope _ = new(rootDirectory.FullName);
            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "config",
                "--project-file", projectFile.FullName,
                "--key", "username",
                "--value", "alice"
            ]);

            Assert.Equal(0, code);
            Assert.True(File.Exists(configFilePath));
            Assert.Contains("alice", File.ReadAllText(configFilePath), StringComparison.Ordinal);
        }
        finally
        {
            if (rootDirectory.Exists)
            {
                rootDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Config_GlobalOutsideProject_SucceedsForSetReadAndList()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo nonProjectDirectory = Directory.CreateDirectory(rootPath);
        FileInfo globalConfigFile = new(Path.Combine(nonProjectDirectory.FullName, VeelingConfig.ConfigFileName));

        try
        {
            using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider(globalConfigFile: globalConfigFile);
            App app = serviceProvider.GetRequiredService<App>();

            using CurrentDirectoryScope _ = new(nonProjectDirectory.FullName);

            using (ConsoleCapture setConsole = new())
            {
                int setCode = await app.RunAsync([
                    "config",
                    "--global",
                    "--key", "username",
                    "--value", "alice"
                ]);
                Assert.Equal(0, setCode);
                Assert.Equal(string.Empty, setConsole.StdErr.ToString());
            }

            using (ConsoleCapture getConsole = new())
            {
                int getCode = await app.RunAsync([
                    "config",
                    "--global",
                    "--key", "username"
                ]);
                Assert.Equal(0, getCode);
                Assert.Contains("alice", getConsole.StdOut.ToString(), StringComparison.Ordinal);
            }

            using (ConsoleCapture listConsole = new())
            {
                int listCode = await app.RunAsync([
                    "config",
                    "--global"
                ]);
                Assert.Equal(0, listCode);
                Assert.Contains("username = alice", listConsole.StdOut.ToString(), StringComparison.Ordinal);
            }
        }
        finally
        {
            if (nonProjectDirectory.Exists)
            {
                nonProjectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Config_OutsideProjectWithoutGlobal_FailsWithActionableError()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo nonProjectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            using CurrentDirectoryScope _ = new(nonProjectDirectory.FullName);
            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "config",
                "--key", "username"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("No project found", console.StdErr.ToString(), StringComparison.Ordinal);
            Assert.Contains("--global", console.StdErr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (nonProjectDirectory.Exists)
            {
                nonProjectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Config_LocalAndGlobalTogether_ReturnsClearError()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory);
            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "config",
                "--project-file", projectFile.FullName,
                "--local",
                "--global",
                "--key", "username",
                "--value", "alice"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Cannot use --local and --global together", console.StdErr.ToString(), StringComparison.Ordinal);
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
    public async Task Modify_FailsWhenValueAndStdinAreBothSpecified()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory);
        TestProjectFactory.CreateSchemaFile(projectDirectory);

        try
        {
            using ConsoleCapture console = new("\"ignored\"");

            int code = await app.RunAsync([
                "modify",
                "--project-file", projectFile.FullName,
                "Schema1.Field1:en",
                "--value", "Hello",
                "--stdin",
                "--by", "tester"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Specify either --value or --stdin, not both.", console.StdErr.ToString(), StringComparison.Ordinal);
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
    public async Task Modify_WhenPersistenceFails_ReturnsNonZeroAndActionableError()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en"], "en");
        TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

        try
        {
            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "modify",
                "--project-file", projectFile.FullName,
                "Schema1.Field1:en",
                "--value", "Hello",
                "--by", "tester"
            ]);

            Assert.Equal(1, code);
            Assert.Contains(
                "Failed to persist modify command changes",
                console.StdErr.ToString(),
                StringComparison.Ordinal
            );
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
    public async Task Status_WithNoIssues_PrintsSuccessMessage()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

            DataModel masterRecord = new()
            {
                Name = "Field1",
                Value = "Hello"
            };

            DataMetaModel translationMeta = new()
            {
                Status = DataStatus.Approved
            };
            translationMeta.UpdateSourceHash(new Language("en"), "Field1", "Hello");

            DataModel translationRecord = new()
            {
                Name = "Field1",
                Value = "Hei",
                Meta = translationMeta
            };

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", masterRecord);
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", translationRecord);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "status",
                "--project-file", projectFile.FullName
            ]);

            Assert.Equal(0, code);
            Assert.Contains("No issues found in 'TestProject'.", console.StdOut.ToString(), StringComparison.Ordinal);
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
    public async Task Status_WithMissingTranslation_PrintsIssueAndReturnsNonZero()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

            DataModel masterRecord = new()
            {
                Name = "Field1",
                Value = "Hello"
            };

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", masterRecord);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "status",
                "--project-file", projectFile.FullName
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Missing:", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("Schema1.Field1:fi", console.StdOut.ToString(), StringComparison.Ordinal);
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
    public async Task Export_JsonFormat_PrintsJsonPayload()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

            DataModel record = new()
            {
                Name = "Field1",
                Value = "Hello"
            };

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", record);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "export",
                "--project-file", projectFile.FullName,
                "--format", "json",
                "Schema1.Field1:en"
            ]);

            Assert.Equal(0, code);

            using JsonDocument json = JsonDocument.Parse(console.StdOut.ToString());
            Assert.Equal(
                "Hello",
                json.RootElement.GetProperty("Schema1.Field1:en").GetString()
            );
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
    public async Task Export_WithoutSelector_ExportsAllRecordsLikeWildcardSelector()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema2", "FieldA");

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new DataModel { Name = "Field1", Value = "Hei" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "en", new DataModel { Name = "FieldA", Value = "Alpha" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "fi", new DataModel { Name = "FieldA", Value = "Alfa" });

            string defaultPayload;
            using (ConsoleCapture console = new())
            {
                int code = await app.RunAsync([
                    "export",
                    "--project-file", projectFile.FullName,
                    "--format", "json"
                ]);

                Assert.Equal(0, code);
                defaultPayload = console.StdOut.ToString();
            }

            string wildcardPayload;
            using (ConsoleCapture console = new())
            {
                int code = await app.RunAsync([
                    "export",
                    "--project-file", projectFile.FullName,
                    "--format", "json",
                    "*.*:*"
                ]);

                Assert.Equal(0, code);
                wildcardPayload = console.StdOut.ToString();
            }

            using JsonDocument defaultJson = JsonDocument.Parse(defaultPayload);
            using JsonDocument wildcardJson = JsonDocument.Parse(wildcardPayload);

            Assert.True(defaultJson.RootElement.TryGetProperty("Schema1.Field1:en", out _));
            Assert.True(defaultJson.RootElement.TryGetProperty("Schema1.Field1:fi", out _));
            Assert.True(defaultJson.RootElement.TryGetProperty("Schema2.FieldA:en", out _));
            Assert.True(defaultJson.RootElement.TryGetProperty("Schema2.FieldA:fi", out _));

            Assert.Equal(
                wildcardJson.RootElement.GetRawText(),
                defaultJson.RootElement.GetRawText());
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
    public async Task Export_SchemaSelector_NarrowsToMatchingSchema()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema2", "FieldA");

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new DataModel { Name = "Field1", Value = "Hei" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "en", new DataModel { Name = "FieldA", Value = "Alpha" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "fi", new DataModel { Name = "FieldA", Value = "Alfa" });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "export",
                "--project-file", projectFile.FullName,
                "--format", "json",
                "Schema1.*:*"
            ]);

            Assert.Equal(0, code);

            using JsonDocument json = JsonDocument.Parse(console.StdOut.ToString());
            Assert.True(json.RootElement.TryGetProperty("Schema1.Field1:en", out _));
            Assert.True(json.RootElement.TryGetProperty("Schema1.Field1:fi", out _));
            Assert.False(json.RootElement.TryGetProperty("Schema2.FieldA:en", out _));
            Assert.False(json.RootElement.TryGetProperty("Schema2.FieldA:fi", out _));
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
    public async Task Export_LanguageSelector_NarrowsToMatchingLanguage()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema2", "FieldA");

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new DataModel { Name = "Field1", Value = "Hei" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "en", new DataModel { Name = "FieldA", Value = "Alpha" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema2", "fi", new DataModel { Name = "FieldA", Value = "Alfa" });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "export",
                "--project-file", projectFile.FullName,
                "--format", "json",
                "*.*:en"
            ]);

            Assert.Equal(0, code);

            using JsonDocument json = JsonDocument.Parse(console.StdOut.ToString());
            Assert.True(json.RootElement.TryGetProperty("Schema1.Field1:en", out _));
            Assert.True(json.RootElement.TryGetProperty("Schema2.FieldA:en", out _));
            Assert.False(json.RootElement.TryGetProperty("Schema1.Field1:fi", out _));
            Assert.False(json.RootElement.TryGetProperty("Schema2.FieldA:fi", out _));
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
    public async Task Export_NoMatchSelector_YamlAndJsonReturnEmptyPayloadWithSuccess()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });

            string yamlPayload;
            using (ConsoleCapture console = new())
            {
                int code = await app.RunAsync([
                    "export",
                    "--project-file", projectFile.FullName,
                    "NoSuchSchema.*:*"
                ]);

                Assert.Equal(0, code);
                yamlPayload = console.StdOut.ToString();
            }

            string jsonPayload;
            using (ConsoleCapture console = new())
            {
                int code = await app.RunAsync([
                    "export",
                    "--project-file", projectFile.FullName,
                    "--format", "json",
                    "NoSuchSchema.*:*"
                ]);

                Assert.Equal(0, code);
                jsonPayload = console.StdOut.ToString();
            }

            Assert.Equal("{}", yamlPayload.Trim());

            using JsonDocument json = JsonDocument.Parse(jsonPayload);
            Assert.Empty(json.RootElement.EnumerateObject());
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
    public async Task Export_InvalidSelector_ReturnsValidationErrorAndNonZeroExitCode()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new DataModel { Name = "Field1", Value = "Hello" });

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "export",
                "--project-file", projectFile.FullName,
                "invalid"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Expected format: <schema>.<field>:<lang>", console.StdErr.ToString(), StringComparison.Ordinal);
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
    public async Task Publish_UsesMasterFallbackForMissingTargetLanguageData()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");

            DataModel masterRecord = new()
            {
                Name = "Field1",
                Value = "Hello"
            };

            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", masterRecord);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "publish",
                "--project-file", projectFile.FullName
            ]);

            Assert.Equal(0, code);

            using JsonDocument json = JsonDocument.Parse(console.StdOut.ToString());

            Assert.Equal(
                "Hello",
                json.RootElement.GetProperty("en")
                    .GetProperty("Schema1")
                    .GetProperty("Field1")
                    .GetString()
            );

            Assert.Equal(
                "Hello",
                json.RootElement.GetProperty("fi")
                    .GetProperty("Schema1")
                    .GetProperty("Field1")
                    .GetString()
            );
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
    public async Task Publish_WithoutMasterData_ReturnsCommandError()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.CommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.EnsureDataDirectory(projectDirectory);

            using ConsoleCapture console = new();

            int code = await app.RunAsync([
                "publish",
                "--project-file", projectFile.FullName
            ]);

            Assert.Equal(1, code);
            Assert.Contains("master language 'en' does not exist", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }
}
