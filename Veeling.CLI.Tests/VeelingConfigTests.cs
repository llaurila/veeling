using Veeling.CLI.Exceptions;

namespace Veeling.CLI.Tests;

public class VeelingConfigTests : IDisposable
{
    private readonly DirectoryInfo projectDir;
    private readonly FileInfo sandboxGlobalConfigFile;
    private readonly FileInfo realGlobalConfigFile;

    public VeelingConfigTests()
    {
        string path = Path.GetTempPath();

        projectDir = new DirectoryInfo(Path.Combine(path, Guid.NewGuid().ToString()));
        projectDir.Create();

        sandboxGlobalConfigFile = new FileInfo(Path.Combine(path, $".veelingconfig.{Guid.NewGuid()}.yaml"));
        realGlobalConfigFile = new FileInfo(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                VeelingConfig.ConfigFileName
            )
        );
    }

    public void Dispose()
    {
        projectDir.Delete(true);

        if (sandboxGlobalConfigFile.Exists)
        {
            sandboxGlobalConfigFile.Delete();
        }
    }

    private VeelingConfig CreateConfig(DirectoryInfo? directory = null)
    {
        return new VeelingConfig(directory ?? projectDir, new FixedGlobalConfigFileLocator(sandboxGlobalConfigFile));
    }

    [Fact]
    public void GetValue_ReturnsLocalValue_WhenKeyExistsLocally()
    {
        VeelingConfig config = CreateConfig(projectDir);
        config.SetLocalValue("username", "test");
        string? value = config.GetValue("username");
        Assert.Equal("test", value);
    }

    [Fact]
    public void GetValue_ReturnsGlobalValue_WhenKeyExistsGlobally()
    {
        VeelingConfig config = CreateConfig(projectDir);
        config.SetGlobalValue("username", "globaltest");
        string? value = config.GetValue("username");
        Assert.Equal("globaltest", value);
    }

    [Fact]
    public void GetValue_ReturnsLocalValue_WhenKeyExistsBothLocallyAndGlobally()
    {
        VeelingConfig config = CreateConfig(projectDir);
        config.SetGlobalValue("username", "globaltest");
        config.SetLocalValue("username", "localtest");
        string? value = config.GetValue("username");
        Assert.Equal("localtest", value);
    }

    [Fact]
    public void InvalidKey_ThrowsArgumentException()
    {
        VeelingConfig config = CreateConfig(projectDir);
        Assert.Throws<ArgumentException>(() => config.SetLocalValue("invalidkey", "value"));
        Assert.Throws<ArgumentException>(() => config.SetGlobalValue("invalidkey", "value"));
        Assert.Throws<ArgumentException>(() => config.GetValue("invalidkey"));
    }

    [Theory]
    [InlineData("username")]
    [InlineData("openai_model")]
    [InlineData("openai_apikey")]
    [InlineData("gemini_model")]
    [InlineData("gemini_apikey")]
    [InlineData("claude_model")]
    [InlineData("claude_apikey")]
    [InlineData("claude_max_tokens")]
    [InlineData("llm_provider")]
    [InlineData("intent_parser_provider")]
    [InlineData("intent_parser_model")]
    public void ValidKeys_AreAccepted(string key)
    {
        VeelingConfig config = CreateConfig(projectDir);
        config.SetLocalValue(key, "value");

        string? value = config.GetValue(key);

        Assert.Equal("value", value);
    }

    [Fact]
    public void SetGlobalValue_WhenConfigDirectoryMissing_ThrowsPersistenceExceptionWithFileContext()
    {
        string configDirPath = Path.Combine(projectDir.FullName, "global-config");
        Directory.CreateDirectory(configDirPath);
        string filePath = Path.Combine(configDirPath, "global.yaml");

        VeelingConfig config = new(projectDir, new FixedGlobalConfigFileLocator(new FileInfo(filePath)));

        Directory.Delete(configDirPath, recursive: true);

        PersistenceException ex = Assert.Throws<PersistenceException>(() => config.SetGlobalValue("username", "alice"));

        Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
        Assert.IsType<DirectoryNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void SetGlobalValue_UsesSandboxedLocator_DoesNotTouchRealHomeConfigPath()
    {
        DateTimeOffset? realLastWriteBefore = realGlobalConfigFile.Exists
            ? realGlobalConfigFile.LastWriteTimeUtc
            : null;

        VeelingConfig config = CreateConfig(projectDir);
        Assert.Equal(sandboxGlobalConfigFile.FullName, config.GlobalConfigFile.FullName);
        config.SetGlobalValue("username", "sandbox-user");

        Assert.True(File.Exists(sandboxGlobalConfigFile.FullName));
        Assert.Contains("sandbox-user", File.ReadAllText(sandboxGlobalConfigFile.FullName), StringComparison.Ordinal);

        if (realLastWriteBefore is null)
        {
            Assert.False(realGlobalConfigFile.Exists);
        }
        else
        {
            realGlobalConfigFile.Refresh();
            Assert.Equal(realLastWriteBefore.Value, realGlobalConfigFile.LastWriteTimeUtc);
        }
    }

    private sealed class FixedGlobalConfigFileLocator(FileInfo file) : IGlobalConfigFileLocator
    {
        public FileInfo GetGlobalConfigFile() => file;
    }
}
