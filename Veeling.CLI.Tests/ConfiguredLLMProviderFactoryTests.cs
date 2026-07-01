using Veeling.CLI.Providers;

namespace Veeling.CLI.Tests;

public class ConfiguredLLMProviderFactoryTests
{
    [Fact]
    public void Create_WithOpenAiLocalConfig_ReturnsOpenAiProvider()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("llm_provider", "openai");
            config.SetLocalValue("openai_model", "gpt-4.1-mini");
            config.SetLocalValue("openai_apikey", "test-openai-key");

            ConfiguredLLMProviderFactory factory = new();

            ILLMProvider provider = factory.Create(projectDirectory);

            Assert.IsType<OpenAIProvider>(provider);
        }
        finally
        {
            projectDirectory.Delete(true);
        }
    }

    [Fact]
    public void Create_WithGeminiLocalConfig_ReturnsGeminiProvider()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("llm_provider", "gemini");
            config.SetLocalValue("gemini_model", "gemini-2.5-flash");
            config.SetLocalValue("gemini_apikey", "test-gemini-key");

            ConfiguredLLMProviderFactory factory = new();

            ILLMProvider provider = factory.Create(projectDirectory);

            Assert.IsType<GeminiProvider>(provider);
        }
        finally
        {
            projectDirectory.Delete(true);
        }
    }

    [Fact]
    public void Create_WithClaudeLocalConfig_ReturnsClaudeProvider()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("llm_provider", "claude");
            config.SetLocalValue("claude_model", "claude-sonnet-4-5");
            config.SetLocalValue("claude_apikey", "test-claude-key");
            config.SetLocalValue("claude_max_tokens", "2048");

            ConfiguredLLMProviderFactory factory = new();

            ILLMProvider provider = factory.Create(projectDirectory);

            Assert.IsType<ClaudeProvider>(provider);
        }
        finally
        {
            projectDirectory.Delete(true);
        }
    }

    [Fact]
    public void Create_WithUnknownProvider_ThrowsInvalidOperationException()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("llm_provider", "unknown");

            ConfiguredLLMProviderFactory factory = new();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.Create(projectDirectory));

            Assert.Contains("Supported values: openai, gemini, claude", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            projectDirectory.Delete(true);
        }
    }

    [Fact]
    public void Create_UsesLocalConfigPerProjectDirectory()
    {
        DirectoryInfo openAiProject = CreateTempProjectDirectory();
        DirectoryInfo geminiProject = CreateTempProjectDirectory();

        try
        {
            VeelingConfig openAiConfig = new(openAiProject);
            openAiConfig.SetLocalValue("llm_provider", "openai");
            openAiConfig.SetLocalValue("openai_model", "gpt-4.1-mini");
            openAiConfig.SetLocalValue("openai_apikey", "test-openai-key");

            VeelingConfig geminiConfig = new(geminiProject);
            geminiConfig.SetLocalValue("llm_provider", "gemini");
            geminiConfig.SetLocalValue("gemini_model", "gemini-2.5-flash");
            geminiConfig.SetLocalValue("gemini_apikey", "test-gemini-key");

            ConfiguredLLMProviderFactory factory = new();

            ILLMProvider openAiProvider = factory.Create(openAiProject);
            ILLMProvider geminiProvider = factory.Create(geminiProject);

            Assert.IsType<OpenAIProvider>(openAiProvider);
            Assert.IsType<GeminiProvider>(geminiProvider);
        }
        finally
        {
            openAiProject.Delete(true);
            geminiProject.Delete(true);
        }
    }

    [Fact]
    public void Create_WithBlankProvider_UsesOpenAiDefault()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("llm_provider", "   ");
            config.SetLocalValue("openai_model", "gpt-4.1-mini");
            config.SetLocalValue("openai_apikey", "test-openai-key");

            ConfiguredLLMProviderFactory factory = new();

            ILLMProvider provider = factory.Create(projectDirectory);

            Assert.IsType<OpenAIProvider>(provider);
        }
        finally
        {
            projectDirectory.Delete(true);
        }
    }

    private static DirectoryInfo CreateTempProjectDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "Veeling.LLMProviderFactoryTests",
            Guid.NewGuid().ToString("N")
        );

        return Directory.CreateDirectory(path);
    }
}
