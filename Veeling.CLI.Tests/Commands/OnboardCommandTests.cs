using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.CLI.Providers;

namespace Veeling.CLI.Tests.Commands;

public sealed class OnboardCommandTests
{
    [Fact]
    public async Task Onboard_HappyPath_OpenAi_DefaultModel_WritesGlobalConfigAndPrintsSuccess()
    {
        DirectoryInfo sandbox = CreateSandbox();
        FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);

        try
        {
            using ServiceProvider serviceProvider = CreateServiceProvider(new SucceedingProviderFactory(), globalConfigFile);
            App app = serviceProvider.GetRequiredService<App>();

            using ConsoleCapture console = new(string.Join(Environment.NewLine,
                "1", // provider openai
                "1", // model gpt-4.1-mini
                "sk-openai-123" // api key
            ) + Environment.NewLine);

            int code = await app.RunAsync(["onboard"]);

            Assert.Equal(0, code);
            Assert.Contains("Provider: openai", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("API key: [redacted]", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("onboarding complete", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sk-openai-123", console.StdOut.ToString(), StringComparison.Ordinal);

            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));
            Assert.Equal("openai", config.GetValue("llm_provider"));
            Assert.Equal("gpt-4.1-mini", config.GetValue("openai_model"));
            Assert.Equal("sk-openai-123", config.GetValue("openai_apikey"));

            DirectoryInfo projectDirectory = Directory.CreateDirectory(Path.Combine(sandbox.FullName, "project"));
            ILLMProvider resolvedProvider = new ConfiguredLLMProviderFactory(new FixedGlobalConfigFileLocator(globalConfigFile)).Create(projectDirectory);
            Assert.IsType<OpenAIProvider>(resolvedProvider);
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Onboard_Claude_OtherModel_InvalidThenValidMaxTokens_WritesClaudeConfig()
    {
        DirectoryInfo sandbox = CreateSandbox();
        FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);

        try
        {
            using ServiceProvider serviceProvider = CreateServiceProvider(new SucceedingProviderFactory(), globalConfigFile);
            App app = serviceProvider.GetRequiredService<App>();

            using ConsoleCapture console = new(string.Join(Environment.NewLine,
                "3", // provider claude
                "4", // model Other
                "claude-custom-1", // custom model
                "sk-claude-123", // api key
                "0", // invalid max tokens
                "2048" // valid
            ) + Environment.NewLine);

            int code = await app.RunAsync(["onboard"]);

            Assert.Equal(0, code);
            Assert.Contains("Claude max tokens must be a positive integer.", console.StdOut.ToString(), StringComparison.Ordinal);

            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));
            Assert.Equal("claude", config.GetValue("llm_provider"));
            Assert.Equal("claude-custom-1", config.GetValue("claude_model"));
            Assert.Equal("sk-claude-123", config.GetValue("claude_apikey"));
            Assert.Equal("2048", config.GetValue("claude_max_tokens"));
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Onboard_RePromptsForInvalidProviderAndBlankApiKey()
    {
        DirectoryInfo sandbox = CreateSandbox();
        FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);

        try
        {
            using ServiceProvider serviceProvider = CreateServiceProvider(new SucceedingProviderFactory(), globalConfigFile);
            App app = serviceProvider.GetRequiredService<App>();

            using ConsoleCapture console = new(string.Join(Environment.NewLine,
                "9", // invalid provider
                "2", // gemini
                "1", // gemini-2.5-flash
                "", // blank api key
                "gem-key-123" // valid api key
            ) + Environment.NewLine);

            int code = await app.RunAsync(["onboard"]);

            Assert.Equal(0, code);
            Assert.Contains("Invalid input. Please enter a valid number.", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("API key cannot be empty.", console.StdOut.ToString(), StringComparison.Ordinal);

            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));
            Assert.Equal("gemini", config.GetValue("llm_provider"));
            Assert.Equal("gemini-2.5-flash", config.GetValue("gemini_model"));
            Assert.Equal("gem-key-123", config.GetValue("gemini_apikey"));
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    [Fact]
    public async Task Onboard_AuthFailure_ReturnsNonZeroAndRedactedOutput()
    {
        DirectoryInfo sandbox = CreateSandbox();
        FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);

        try
        {
            using ServiceProvider serviceProvider = CreateServiceProvider(new FailingProviderFactory("Unauthorized: invalid api key"), globalConfigFile);
            App app = serviceProvider.GetRequiredService<App>();

            using ConsoleCapture console = new(string.Join(Environment.NewLine,
                "1", // openai
                "1", // model
                "secret-openai-key"
            ) + Environment.NewLine);

            int code = await app.RunAsync(["onboard"]);

            Assert.Equal(1, code);
            Assert.Contains("authentication", console.StdErr.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-openai-key", console.StdOut.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret-openai-key", console.StdErr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    private static DirectoryInfo CreateSandbox()
    {
        string path = Path.Combine(Path.GetTempPath(), "Veeling.OnboardCommandTests", Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path);
    }

    private static FileInfo CreateGlobalConfigFile(DirectoryInfo sandbox)
    {
        string path = Path.Combine(sandbox.FullName, "global.veeling.yaml");
        return new FileInfo(path);
    }

    private static ServiceProvider CreateServiceProvider(ILLMProviderFactory providerFactory, FileInfo globalConfigFile)
    {
        ServiceCollection services = new();
        services.AddVeelingCli(new ConfigurationBuilder().Build());
        services.AddSingleton(providerFactory);
        services.AddSingleton<IGlobalConfigFileLocator>(new FixedGlobalConfigFileLocator(globalConfigFile));
        return services.BuildServiceProvider();
    }

    private sealed class FixedGlobalConfigFileLocator(FileInfo file) : IGlobalConfigFileLocator
    {
        public FileInfo GetGlobalConfigFile() => file;
    }

    private sealed class SucceedingProviderFactory : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            return new SucceedingProvider();
        }
    }

    private sealed class SucceedingProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new(LLMChatMessageRole.Assistant, "VEELING_OK");
        }
    }

    private sealed class FailingProviderFactory(string message) : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            return new FailingProvider(message);
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
