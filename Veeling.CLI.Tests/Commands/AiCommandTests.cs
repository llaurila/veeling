using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.CLI.Commands;
using Veeling.CLI.Providers;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Commands;

public sealed class AiCommandTests
{
    [Fact]
    public async Task Ai_WhenResolvedAndUserDeclines_DoesNotDispatch()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(
                Path: ["status"],
                Options: null,
                Arguments: [],
                SuggestionOnly: false,
                SuggestionReason: null),
            Commands: null,
            Explanation: "status check",
            RequiresConfirmation: true)));

        App app = serviceProvider.GetRequiredService<App>();

        using ConsoleCapture console = new("n\n");
        int code = await app.RunAsync(["ai", "show status"]);

        Assert.Equal(1, code);
        Assert.Contains("Resolved command preview", console.StdOut.ToString(), StringComparison.Ordinal);
        Assert.Contains("veeling status", console.StdOut.ToString(), StringComparison.Ordinal);
        Assert.Contains("Aborted by user", console.StdErr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAlias_WorksAndDispatchesOnConfirmation()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(
                Path: ["status"],
                Options: null,
                Arguments: [],
                SuggestionOnly: false,
                SuggestionReason: null),
            Commands: null,
            Explanation: "status check",
            RequiresConfirmation: true)));

        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.AiCommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new Veeling.Models.DataModel
            {
                Name = "Field1",
                Value = "Hello"
            });

            using ConsoleCapture console = new("y\n");
            int code = await app.RunAsync(["ask", "show status", "--project-file", projectFile.FullName]);

            Assert.Equal(0, code);
            Assert.Contains("Resolved command preview", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("No issues found", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
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
    public async Task Ai_WhenResolvedSuggestionOnly_PrintsSuggestionAndDoesNotPrompt()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(
                Path: ["onboard"],
                Options: null,
                Arguments: [],
                SuggestionOnly: false,
                SuggestionReason: "Use onboard manually."),
            Commands: null,
            Explanation: "setup",
            RequiresConfirmation: true)));

        App app = serviceProvider.GetRequiredService<App>();

        using ConsoleCapture console = new();
        int code = await app.RunAsync(["ai", "set up ai"]);

        Assert.Equal(0, code);
        Assert.Contains("Resolved command preview", console.StdOut.ToString(), StringComparison.Ordinal);
        Assert.Contains("Use onboard manually", console.StdOut.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ai_TranslationStyleIntent_DispatchesTranslateDryRunWhenConfirmed()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(
                Path: ["translate"],
                Options: new Dictionary<string, string?>
                {
                    ["--to"] = "fi",
                    ["--dry-run"] = null
                },
                Arguments: [],
                SuggestionOnly: false,
                SuggestionReason: null),
            Commands: null,
            Explanation: "translate all",
            RequiresConfirmation: true)));

        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.AiCommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new Veeling.Models.DataModel { Name = "Field1", Value = "Hello" });
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "fi", new Veeling.Models.DataModel { Name = "Field1", Value = "Hei" });

            using ConsoleCapture console = new("y\n");
            int code = await app.RunAsync(["ai", "translate everything to fi", "--project-file", projectFile.FullName]);

            Assert.Equal(0, code);
            Assert.Contains("Resolved command preview", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("All fields are already translated, skipping.", console.StdOut.ToString(), StringComparison.Ordinal);
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
    public async Task Ai_ModifyWildcardIntent_RequiresConfirmationBeforeDispatch()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(
                Path: ["modify"],
                Options: new Dictionary<string, string?>
                {
                    ["--status"] = "Approved",
                    ["--by"] = "agent",
                    ["--force"] = null
                },
                Arguments: ["Schema1.*:en"],
                SuggestionOnly: false,
                SuggestionReason: null),
            Commands: null,
            Explanation: "bulk approve",
            RequiresConfirmation: true)));

        App app = serviceProvider.GetRequiredService<App>();

        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.AiCommandTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en"], "en");
            TestProjectFactory.CreateSchemaFile(projectDirectory, "Schema1", "Field1");
            TestProjectFactory.CreateDataFile(projectDirectory, "Schema1", "en", new Veeling.Models.DataModel { Name = "Field1", Value = "Hello" });

            using ConsoleCapture console = new("n\n");
            int code = await app.RunAsync(["ai", "approve all en records", "--project-file", projectFile.FullName]);

            Assert.Equal(1, code);
            Assert.Contains("veeling modify", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("Execute this command?", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("Aborted by user", console.StdErr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    private static ServiceProvider CreateServiceProvider(IIntentParserProvider parserProvider)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddVeelingCli(configuration);
        services.AddSingleton(parserProvider);
        return services.BuildServiceProvider();
    }

    private sealed class FakeIntentParserProvider(IntentParserResponse response) : IIntentParserProvider
    {
        public IntentParserResponse Parse(IntentParserProviderSelection selection, IntentParserRequest request) => response;
    }
}
