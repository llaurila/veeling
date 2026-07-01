using Veeling.CLI;
using Veeling.CLI.Providers;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class IntentResolutionApplicationServiceTests : IDisposable
{
    private readonly DirectoryInfo testRoot;
    private readonly FileInfo globalConfigFile;

    public IntentResolutionApplicationServiceTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "Veeling.IntentResolutionTests", Guid.NewGuid().ToString("N"));
        testRoot = Directory.CreateDirectory(root);
        globalConfigFile = new FileInfo(Path.Combine(testRoot.FullName, ".global.veeling.yaml"));
    }

    public void Dispose()
    {
        if (testRoot.Exists)
        {
            testRoot.Delete(true);
        }
    }

    [Fact]
    public void Resolve_RejectsRecursiveAiTarget()
    {
        CommandCatalog catalog = TestCatalog();
        IntentResolutionApplicationService service = CreateService(catalog, new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(["ai"], null, null, null, null),
            Commands: null,
            Explanation: null,
            RequiresConfirmation: true)));

        FileInfo projectFile = new(Path.Combine(testRoot.FullName, "Project.yaml"));
        IntentResolutionResult result = service.Resolve(new IntentResolutionRequest("do a thing", projectFile));

        Assert.Equal(IntentResolutionOutcome.Unsupported, result.Outcome);
        Assert.Contains("Recursive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RejectsMultiCommandPayload()
    {
        CommandCatalog catalog = TestCatalog();
        IntentResolutionApplicationService service = CreateService(catalog, new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(["translate"], new Dictionary<string, string?> { ["--to"] = "pt" }, [], false, null),
            Commands: [new IntentParserCommandSpec(["status"], null, null, null, null)],
            Explanation: null,
            RequiresConfirmation: true)));

        FileInfo projectFile = new(Path.Combine(testRoot.FullName, "Project.yaml"));
        IntentResolutionResult result = service.Resolve(new IntentResolutionRequest("translate and status", projectFile));

        Assert.Equal(IntentResolutionOutcome.Unsupported, result.Outcome);
        Assert.Contains("single-command", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RejectsUnknownOption()
    {
        CommandCatalog catalog = TestCatalog();
        IntentResolutionApplicationService service = CreateService(catalog, new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(["translate"], new Dictionary<string, string?> { ["--unknown"] = "x" }, [], false, null),
            Commands: null,
            Explanation: null,
            RequiresConfirmation: true)));

        FileInfo projectFile = new(Path.Combine(testRoot.FullName, "Project.yaml"));
        IntentResolutionResult result = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal(IntentResolutionOutcome.Unsupported, result.Outcome);
        Assert.Contains("Unknown option", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RejectsResolvedOutcomeWithoutCommand()
    {
        IntentResolutionApplicationService service = CreateService(TestCatalog(), new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: null,
            Commands: null,
            Explanation: null,
            RequiresConfirmation: true)));

        FileInfo projectFile = new(Path.Combine(testRoot.FullName, "Project.yaml"));
        IntentResolutionResult result = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal(IntentResolutionOutcome.Unsupported, result.Outcome);
        Assert.Contains("exactly one command", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_UsesIntentParserConfigFallbackToLlmProvider()
    {
        DirectoryInfo projectDir = Directory.CreateDirectory(Path.Combine(testRoot.FullName, "project-a"));
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDir, ["en", "pt"], "en");

        VeelingConfig config = new(projectDir, new FixedGlobalConfigFileLocator(globalConfigFile));
        config.SetLocalValue("llm_provider", "gemini");
        config.SetLocalValue("intent_parser_model", "gemini-2.5-flash");

        RecordingIntentParserProvider parser = new(new IntentParserResponse(
            Outcome: "clarification",
            Message: "Need scope",
            Command: null,
            Commands: null,
            Explanation: null,
            RequiresConfirmation: null));

        IntentResolutionApplicationService service = CreateService(TestCatalog(), parser);
        _ = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal("gemini", parser.LastSelection?.Provider);
        Assert.Equal("gemini-2.5-flash", parser.LastSelection?.Model);
    }

    [Fact]
    public void Resolve_ParserProviderOverridesLlmProvider()
    {
        DirectoryInfo projectDir = Directory.CreateDirectory(Path.Combine(testRoot.FullName, "project-b"));
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDir, ["en", "pt"], "en");

        VeelingConfig config = new(projectDir, new FixedGlobalConfigFileLocator(globalConfigFile));
        config.SetLocalValue("llm_provider", "openai");
        config.SetLocalValue("intent_parser_provider", "claude");

        RecordingIntentParserProvider parser = new(new IntentParserResponse(
            Outcome: "clarification",
            Message: "Need scope",
            Command: null,
            Commands: null,
            Explanation: null,
            RequiresConfirmation: null));

        IntentResolutionApplicationService service = CreateService(TestCatalog(), parser);
        _ = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal("claude", parser.LastSelection?.Provider);
    }

    [Fact]
    public void Resolve_IntentParserModelUnset_LeavesModelNullForProviderDefault()
    {
        DirectoryInfo projectDir = Directory.CreateDirectory(Path.Combine(testRoot.FullName, "project-c"));
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDir, ["en", "pt"], "en");

        VeelingConfig config = new(projectDir, new FixedGlobalConfigFileLocator(globalConfigFile));
        config.SetLocalValue("llm_provider", "openai");

        RecordingIntentParserProvider parser = new(new IntentParserResponse(
            Outcome: "clarification",
            Message: "Need scope",
            Command: null,
            Commands: null,
            Explanation: null,
            RequiresConfirmation: null));

        IntentResolutionApplicationService service = CreateService(TestCatalog(), parser);
        _ = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal("openai", parser.LastSelection?.Provider);
        Assert.Null(parser.LastSelection?.Model);
    }

    [Fact]
    public void Resolve_UsesGlobalFallbackWhenLocalParserKeysUnset()
    {
        DirectoryInfo projectDir = Directory.CreateDirectory(Path.Combine(testRoot.FullName, "project-global"));
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDir, ["en", "pt"], "en");

        VeelingConfig globalOnly = new(projectDir, new FixedGlobalConfigFileLocator(globalConfigFile));
        globalOnly.SetGlobalValue("llm_provider", "gemini");
        globalOnly.SetGlobalValue("intent_parser_model", "gemini-2.0-flash");

        RecordingIntentParserProvider parser = new(new IntentParserResponse(
            Outcome: "clarification",
            Message: "Need scope",
            Command: null,
            Commands: null,
            Explanation: null,
            RequiresConfirmation: null));

        IntentResolutionApplicationService service = CreateService(TestCatalog(), parser);
        _ = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal("gemini", parser.LastSelection?.Provider);
        Assert.Equal("gemini-2.0-flash", parser.LastSelection?.Model);
    }

    [Fact]
    public void Resolve_FlagsOnboardAsSuggestionOnly()
    {
        CommandCatalog catalog = TestCatalog();
        IntentResolutionApplicationService service = CreateService(catalog, new FakeIntentParserProvider(new IntentParserResponse(
            Outcome: "resolved",
            Message: null,
            Command: new IntentParserCommandSpec(["onboard"], null, [], false, null),
            Commands: null,
            Explanation: "setup",
            RequiresConfirmation: true)));

        FileInfo projectFile = new(Path.Combine(testRoot.FullName, "Project.yaml"));
        IntentResolutionResult result = service.Resolve(new IntentResolutionRequest("set up ai", projectFile));

        Assert.Equal(IntentResolutionOutcome.Resolved, result.Outcome);
        Assert.True(result.Command!.IsSuggestionOnly);
    }

    [Fact]
    public void Resolve_BuildsMinimalProjectContext()
    {
        DirectoryInfo projectDir = Directory.CreateDirectory(Path.Combine(testRoot.FullName, "project-context"));
        FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDir, ["en", "fi", "pt"], "en");

        RecordingIntentParserProvider parser = new(new IntentParserResponse(
            Outcome: "clarification",
            Message: "clarify",
            Command: null,
            Commands: null,
            Explanation: null,
            RequiresConfirmation: null));

        IntentResolutionApplicationService service = CreateService(TestCatalog(), parser);
        _ = service.Resolve(new IntentResolutionRequest("translate everything", projectFile));

        Assert.NotNull(parser.LastRequest);
        Assert.True(parser.LastRequest!.ProjectContext.ProjectDetected);
        Assert.Equal("en", parser.LastRequest.ProjectContext.MasterLanguage);
        Assert.Equal(["en", "fi", "pt"], parser.LastRequest.ProjectContext.Languages);
    }

    [Fact]
    public void Resolve_WhenParserThrows_ReturnsUnsupportedWithMessage()
    {
        IntentResolutionApplicationService service = CreateService(TestCatalog(), new ThrowingIntentParserProvider("bad json"));
        FileInfo projectFile = new(Path.Combine(testRoot.FullName, "Project.yaml"));

        IntentResolutionResult result = service.Resolve(new IntentResolutionRequest("translate", projectFile));

        Assert.Equal(IntentResolutionOutcome.Unsupported, result.Outcome);
        Assert.Contains("Intent parser failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private IntentResolutionApplicationService CreateService(CommandCatalog catalog, IIntentParserProvider parser)
    {
        return new IntentResolutionApplicationService(
            new StaticCatalogBuilder(catalog),
            parser,
            new FixedGlobalConfigFileLocator(globalConfigFile));
    }

    private static CommandCatalog TestCatalog()
    {
        return new CommandCatalog(
            Commands:
            [
                new CommandCatalogEntry("ai", "", ["ai"], ["ask"], [], [], "never recurse"),
                new CommandCatalogEntry("translate", "", ["translate"], [],
                [
                    new CommandCatalogOption("--to", "", [], true),
                    new CommandCatalogOption("--dry-run", "", [], false)
                ], [], ""),
                new CommandCatalogEntry("onboard", "", ["onboard"], [], [], [], "suggestion only"),
                new CommandCatalogEntry("status", "", ["status"], [], [], [], "")
            ],
            CuratedHints: new Dictionary<string, string>());
    }

    private sealed class StaticCatalogBuilder(CommandCatalog catalog) : ICommandCatalogBuilder
    {
        public CommandCatalog Build() => catalog;
    }

    private sealed class FakeIntentParserProvider(IntentParserResponse response) : IIntentParserProvider
    {
        public IntentParserResponse Parse(IntentParserProviderSelection selection, IntentParserRequest request) => response;
    }

    private sealed class RecordingIntentParserProvider(IntentParserResponse response) : IIntentParserProvider
    {
        public IntentParserProviderSelection? LastSelection { get; private set; }

        public IntentParserRequest? LastRequest { get; private set; }

        public IntentParserResponse Parse(IntentParserProviderSelection selection, IntentParserRequest request)
        {
            LastSelection = selection;
            LastRequest = request;
            return response;
        }
    }

    private sealed class ThrowingIntentParserProvider(string message) : IIntentParserProvider
    {
        public IntentParserResponse Parse(IntentParserProviderSelection selection, IntentParserRequest request)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedGlobalConfigFileLocator(FileInfo file) : IGlobalConfigFileLocator
    {
        public FileInfo GetGlobalConfigFile() => file;
    }
}
