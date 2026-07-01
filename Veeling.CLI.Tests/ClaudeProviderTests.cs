using Veeling.CLI.Providers;

namespace Veeling.CLI.Tests;

public class ClaudeProviderTests
{
    [Fact]
    public void Constructor_WithInvalidMaxTokens_Throws()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("claude_model", "claude-sonnet-4-5");
            config.SetLocalValue("claude_apikey", "test-claude-key");
            config.SetLocalValue("claude_max_tokens", "0");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => new ClaudeProvider(config));

            Assert.Contains("claude_max_tokens", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("positive integer", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            projectDirectory.Delete(true);
        }
    }

    [Fact]
    public void Constructor_WithMissingMaxTokens_DoesNotThrow()
    {
        DirectoryInfo projectDirectory = CreateTempProjectDirectory();

        try
        {
            VeelingConfig config = new(projectDirectory);
            config.SetLocalValue("claude_model", "claude-sonnet-4-5");
            config.SetLocalValue("claude_apikey", "test-claude-key");

            ClaudeProvider provider = new(config);

            Assert.NotNull(provider);
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
            "Veeling.ClaudeProviderTests",
            Guid.NewGuid().ToString("N")
        );

        return Directory.CreateDirectory(path);
    }
}
