namespace Veeling.CLI.Tests;

public class AtomicFileTests : IDisposable
{
    private readonly DirectoryInfo tempDirectory;

    public AtomicFileTests()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.AtomicFileTests", Guid.NewGuid().ToString("N"));
        tempDirectory = Directory.CreateDirectory(rootPath);
    }

    public void Dispose()
    {
        if (tempDirectory.Exists)
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void WriteAllText_CreatesNewFile()
    {
        FileInfo target = new(Path.Combine(tempDirectory.FullName, "sample.txt"));

        AtomicFile.WriteAllText(target, "hello");

        Assert.True(File.Exists(target.FullName));
        Assert.Equal("hello", File.ReadAllText(target.FullName));
    }

    [Fact]
    public void WriteAllText_ReplacesExistingFileWithoutTempArtifacts()
    {
        FileInfo target = new(Path.Combine(tempDirectory.FullName, "sample.txt"));
        File.WriteAllText(target.FullName, "old");

        AtomicFile.WriteAllText(target, "new");

        Assert.Equal("new", File.ReadAllText(target.FullName));

        string[] tempFiles = Directory
            .GetFiles(tempDirectory.FullName, ".sample.txt.*.tmp", SearchOption.TopDirectoryOnly);

        Assert.Empty(tempFiles);
    }

    [Fact]
    public void WriteAllText_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        DirectoryInfo missingDirectory = new(Path.Combine(tempDirectory.FullName, "missing"));
        FileInfo target = new(Path.Combine(missingDirectory.FullName, "sample.txt"));

        Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.WriteAllText(target, "content"));
    }

    [Fact]
    public void WriteAllText_ReplaceFailure_CleansUpTempFile()
    {
        FileInfo target = new(Path.Combine(tempDirectory.FullName, "sample.txt"));
        File.WriteAllText(target.FullName, "old");

        try
        {
            AtomicFile.ReplaceOperationOverride = (_, _) => throw new IOException("Simulated replace failure.");
            Assert.ThrowsAny<IOException>(() => AtomicFile.WriteAllText(target, "new"));
        }
        finally
        {
            AtomicFile.ReplaceOperationOverride = null;
        }

        string[] tempFiles = Directory
            .GetFiles(tempDirectory.FullName, ".sample.txt.*.tmp", SearchOption.TopDirectoryOnly);

        Assert.Empty(tempFiles);
        Assert.Equal("old", File.ReadAllText(target.FullName));
    }
}
