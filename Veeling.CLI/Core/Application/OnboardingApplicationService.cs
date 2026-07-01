using Veeling.CLI;
using Veeling.CLI.Providers;

namespace Veeling.Core.Application;

public enum OnboardingVerificationStatus
{
    Success,
    AuthenticationFailure,
    ConfigurationFailure,
    ProviderFailure
}

public sealed record OnboardingCommandRequest(
    string Provider,
    string Model,
    string ApiKey,
    int? ClaudeMaxTokens
);

public sealed record OnboardingCommandResult(
    OnboardingVerificationStatus VerificationStatus,
    IReadOnlyList<string> OutputLines,
    IReadOnlyList<string> ErrorLines
);

public sealed class OnboardingApplicationService
{
    private static readonly LLMChatMessage ProbePrompt = new(
        LLMChatMessageRole.User,
        "Reply with exactly: VEELING_OK"
    );

    private readonly ILLMProviderFactory llmProviderFactory;
    private readonly IProviderAuthFailureClassifier authFailureClassifier;
    private readonly Func<VeelingConfig> configFactory;
    private readonly Func<DirectoryInfo> verificationDirectoryFactory;

    public OnboardingApplicationService(
        ILLMProviderFactory llmProviderFactory,
        IProviderAuthFailureClassifier authFailureClassifier,
        IGlobalConfigFileLocator globalConfigFileLocator)
        : this(
            llmProviderFactory,
            authFailureClassifier,
            () => new VeelingConfig(globalConfigFileLocator: globalConfigFileLocator),
            CreateVerificationDirectory)
    {
    }

    public OnboardingApplicationService(
        ILLMProviderFactory llmProviderFactory,
        IProviderAuthFailureClassifier authFailureClassifier,
        Func<VeelingConfig> configFactory,
        Func<DirectoryInfo> verificationDirectoryFactory)
    {
        this.llmProviderFactory = llmProviderFactory;
        this.authFailureClassifier = authFailureClassifier;
        this.configFactory = configFactory;
        this.verificationDirectoryFactory = verificationDirectoryFactory;
    }

    public OnboardingCommandResult Execute(OnboardingCommandRequest request)
    {
        SaveConfig(request);

        List<string> outputLines =
        [
            "Saved AI configuration to global ~/.veeling.yaml.",
            "Verifying provider connectivity..."
        ];

        try
        {
            VerifyConfiguration();

            outputLines.Add($"AI onboarding complete. Provider '{request.Provider}' with model '{request.Model}' is ready.");
            return new OnboardingCommandResult(OnboardingVerificationStatus.Success, outputLines, []);
        }
        catch (Exception ex)
        {
            OnboardingVerificationStatus status = ClassifyFailure(ex);
            string errorLine = status switch
            {
                OnboardingVerificationStatus.AuthenticationFailure =>
                    "Verification failed: authentication rejected by provider. Check your API key and account access.",
                OnboardingVerificationStatus.ConfigurationFailure =>
                    "Verification failed: invalid provider configuration. Check provider, model, and Claude max tokens.",
                _ =>
                    "Verification failed: provider or network is unavailable. Try again shortly.",
            };

            return new OnboardingCommandResult(status, outputLines, [errorLine]);
        }
    }

    private void SaveConfig(OnboardingCommandRequest request)
    {
        VeelingConfig config = configFactory();
        string provider = NormalizeProvider(request.Provider);

        config.SetGlobalValue("llm_provider", provider);
        config.SetGlobalValue(GetModelKey(provider), request.Model.Trim());
        config.SetGlobalValue(GetApiKeyKey(provider), request.ApiKey.Trim());

        if (provider == "claude")
        {
            config.SetGlobalValue("claude_max_tokens", request.ClaudeMaxTokens?.ToString());
        }
    }

    private void VerifyConfiguration()
    {
        DirectoryInfo verificationDirectory = verificationDirectoryFactory();

        try
        {
            ILLMProvider provider = llmProviderFactory.Create(verificationDirectory);
            provider.Complete(ProbePrompt);
        }
        finally
        {
            if (verificationDirectory.Exists)
            {
                verificationDirectory.Delete(recursive: true);
            }
        }
    }

    private static DirectoryInfo CreateVerificationDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "Veeling.Onboard.Verify",
            Guid.NewGuid().ToString("N")
        );

        return Directory.CreateDirectory(path);
    }

    private OnboardingVerificationStatus ClassifyFailure(Exception ex)
    {
        if (authFailureClassifier.IsAuthenticationFailure(ex))
        {
            return OnboardingVerificationStatus.AuthenticationFailure;
        }

        if (LooksLikeConfigurationFailure(ex))
        {
            return OnboardingVerificationStatus.ConfigurationFailure;
        }

        return OnboardingVerificationStatus.ProviderFailure;
    }

    private static bool LooksLikeConfigurationFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is ArgumentException)
            {
                return true;
            }

            string message = current.Message.ToLowerInvariant();
            if (message.Contains("config key", StringComparison.Ordinal)
                || message.Contains("unknown config value", StringComparison.Ordinal)
                || message.Contains("must be a positive integer", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim().ToLowerInvariant();
    }

    private static string GetModelKey(string provider) => provider switch
    {
        "openai" => "openai_model",
        "gemini" => "gemini_model",
        "claude" => "claude_model",
        _ => throw new ArgumentException($"Unsupported provider '{provider}'.", nameof(provider))
    };

    private static string GetApiKeyKey(string provider) => provider switch
    {
        "openai" => "openai_apikey",
        "gemini" => "gemini_apikey",
        "claude" => "claude_apikey",
        _ => throw new ArgumentException($"Unsupported provider '{provider}'.", nameof(provider))
    };
}
