using Veeling.CLI.Providers;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class OnboardingApplicationServiceTests
{
    [Fact]
    public void Execute_HappyPath_PersistsGlobalConfigAndVerifiesProvider()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);
            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));

            RecordingProviderFactory providerFactory = new(new SucceedingProvider());
            OnboardingApplicationService service = CreateService(providerFactory, config);

            OnboardingCommandResult result = service.Execute(
                new OnboardingCommandRequest(
                    Provider: "openai",
                    Model: "gpt-4.1-mini",
                    ApiKey: "sk-test-openai",
                    ClaudeMaxTokens: null
                )
            );

            Assert.Equal(OnboardingVerificationStatus.Success, result.VerificationStatus);
            Assert.Empty(result.ErrorLines);
            Assert.Contains(result.OutputLines, line => line.Contains("Saved AI configuration", StringComparison.Ordinal));
            Assert.Contains(result.OutputLines, line => line.Contains("onboarding complete", StringComparison.OrdinalIgnoreCase));

            string yaml = File.ReadAllText(config.GlobalConfigFile.FullName);
            Assert.Contains("llm_provider: openai", yaml, StringComparison.Ordinal);
            Assert.Contains("openai_model: gpt-4.1-mini", yaml, StringComparison.Ordinal);
            Assert.Contains("openai_apikey: sk-test-openai", yaml, StringComparison.Ordinal);
            Assert.NotNull(providerFactory.LastCreateDirectory);
            Assert.False(providerFactory.LastCreateDirectory!.Exists);
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
    public void Execute_Claude_PersistsClaudeKeysIncludingMaxTokens()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);
            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));

            OnboardingApplicationService service = CreateService(new RecordingProviderFactory(new SucceedingProvider()), config);

            service.Execute(new OnboardingCommandRequest(
                Provider: "claude",
                Model: "claude-sonnet-4-5",
                ApiKey: "sk-test-claude",
                ClaudeMaxTokens: 2048
            ));

            string yaml = File.ReadAllText(config.GlobalConfigFile.FullName);
            Assert.Contains("llm_provider: claude", yaml, StringComparison.Ordinal);
            Assert.Contains("claude_model: claude-sonnet-4-5", yaml, StringComparison.Ordinal);
            Assert.Contains("claude_apikey: sk-test-claude", yaml, StringComparison.Ordinal);
            Assert.Contains("claude_max_tokens: 2048", yaml, StringComparison.Ordinal);
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
    public void Execute_AuthenticationFailure_ReturnsAuthClassification()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);
            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));

            OnboardingApplicationService service = CreateService(
                new RecordingProviderFactory(new ThrowingProvider("Unauthorized: invalid api key")),
                config
            );

            OnboardingCommandResult result = service.Execute(new OnboardingCommandRequest(
                Provider: "gemini",
                Model: "gemini-2.5-flash",
                ApiKey: "secret-key",
                ClaudeMaxTokens: null
            ));

            Assert.Equal(OnboardingVerificationStatus.AuthenticationFailure, result.VerificationStatus);
            Assert.Contains(result.ErrorLines, line => line.Contains("authentication", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("secret-key", string.Join(Environment.NewLine, result.OutputLines.Concat(result.ErrorLines)), StringComparison.Ordinal);
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
    public void Execute_ConfigurationFailure_ReturnsConfigurationClassification()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);
            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));

            OnboardingApplicationService service = CreateService(
                new RecordingProviderFactory(new ThrowingProvider("Config key 'claude_max_tokens' must be a positive integer.")),
                config
            );

            OnboardingCommandResult result = service.Execute(new OnboardingCommandRequest(
                Provider: "claude",
                Model: "claude-sonnet-4-5",
                ApiKey: "secret-key",
                ClaudeMaxTokens: 1
            ));

            Assert.Equal(OnboardingVerificationStatus.ConfigurationFailure, result.VerificationStatus);
            Assert.Contains(result.ErrorLines, line => line.Contains("configuration", StringComparison.OrdinalIgnoreCase));
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
    public void Execute_ProviderFailure_ReturnsProviderClassification()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            FileInfo globalConfigFile = CreateGlobalConfigFile(sandbox);
            VeelingConfig config = new(globalConfigFileLocator: new FixedGlobalConfigFileLocator(globalConfigFile));

            OnboardingApplicationService service = CreateService(
                new RecordingProviderFactory(new ThrowingProvider("provider timeout")),
                config
            );

            OnboardingCommandResult result = service.Execute(new OnboardingCommandRequest(
                Provider: "openai",
                Model: "gpt-4.1-mini",
                ApiKey: "secret-key",
                ClaudeMaxTokens: null
            ));

            Assert.Equal(OnboardingVerificationStatus.ProviderFailure, result.VerificationStatus);
            Assert.Contains(result.ErrorLines, line => line.Contains("network", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    private static OnboardingApplicationService CreateService(RecordingProviderFactory providerFactory, VeelingConfig config)
    {
        return new OnboardingApplicationService(
            providerFactory,
            new ProviderAuthFailureClassifier(),
            () => config,
            CreateVerificationDirectory
        );
    }

    private static DirectoryInfo CreateVerificationDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "Veeling.OnboardingTests.Verify", Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path);
    }

    private static DirectoryInfo CreateSandbox()
    {
        string path = Path.Combine(Path.GetTempPath(), "Veeling.OnboardingTests", Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path);
    }

    private static FileInfo CreateGlobalConfigFile(DirectoryInfo sandbox)
    {
        Directory.CreateDirectory(sandbox.FullName);
        string path = Path.Combine(sandbox.FullName, "global.veeling.yaml");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return new FileInfo(path);
    }

    private sealed class FixedGlobalConfigFileLocator(FileInfo file) : IGlobalConfigFileLocator
    {
        public FileInfo GetGlobalConfigFile() => file;
    }

    private sealed class RecordingProviderFactory(ILLMProvider provider) : ILLMProviderFactory
    {
        public DirectoryInfo? LastCreateDirectory { get; private set; }

        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            LastCreateDirectory = projectDirectory;
            return provider;
        }
    }

    private sealed class SucceedingProvider : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            return new(LLMChatMessageRole.Assistant, "VEELING_OK");
        }
    }

    private sealed class ThrowingProvider(string message) : ILLMProvider
    {
        public LLMChatMessage Complete(params LLMChatMessage[] history)
        {
            throw new InvalidOperationException(message);
        }
    }
}
