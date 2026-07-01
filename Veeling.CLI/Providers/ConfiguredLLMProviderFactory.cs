using Veeling.CLI;

namespace Veeling.CLI.Providers;

public sealed class ConfiguredLLMProviderFactory : ILLMProviderFactory
{
    private const string DefaultProvider = "openai";
    private readonly IGlobalConfigFileLocator globalConfigFileLocator;

    public ConfiguredLLMProviderFactory(IGlobalConfigFileLocator? globalConfigFileLocator = null)
    {
        this.globalConfigFileLocator = globalConfigFileLocator ?? new UserProfileGlobalConfigFileLocator();
    }

    public ILLMProvider Create(DirectoryInfo projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);

        VeelingConfig config = new(projectDirectory, globalConfigFileLocator);
        string providerType = NormalizeProviderType(config.GetValue("llm_provider"));

        return providerType switch
        {
            "openai" => new OpenAIProvider(config),
            "gemini" => new GeminiProvider(config),
            "claude" => new ClaudeProvider(config),
            _ => throw new InvalidOperationException(
                $"Unknown config value 'llm_provider={providerType}'. Supported values: openai, gemini, claude."
            )
        };
    }

    private static string NormalizeProviderType(string? providerType)
    {
        string normalized = providerType?.Trim().ToLowerInvariant() ?? DefaultProvider;
        return string.IsNullOrWhiteSpace(normalized) ? DefaultProvider : normalized;
    }
}
