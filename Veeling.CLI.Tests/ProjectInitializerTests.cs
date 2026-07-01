using Veeling.Models;

namespace Veeling.CLI.Tests;

public class ProjectInitializerTests : IDisposable
{
    private readonly DirectoryInfo tempDirectory;
    private readonly ProjectModel model;

    public ProjectInitializerTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "Veeling.Tests", Guid.NewGuid().ToString("N"));
        tempDirectory = new DirectoryInfo(root);
        model = new ProjectModel
        {
            Name = "Test",
            Description = "Test project",
            MasterLanguage = "en",
            Languages = [new Language("en")],
            Style = new Style { Tone = Tone.Neutral, Formality = Formality.Neutral }
        };
    }

    public void Dispose()
    {
        tempDirectory.Refresh();
        if (tempDirectory.Exists) tempDirectory.Delete(true);
    }

    [Fact]
    public void Initialize_CreatesDirectory()
    {
        var initializer = new ProjectInitializer();

        initializer.Initialize(tempDirectory, model);
        tempDirectory.Refresh();

        Assert.True(tempDirectory.Exists);
    }

    [Fact]
    public void Initialize_RaisesLogEvent()
    {
        var initializer = new ProjectInitializer();
        string? message = null;
        initializer.OnLog += logged => message = logged;

        initializer.Initialize(tempDirectory, model);

        Assert.NotNull(message);
        Assert.Contains(tempDirectory.FullName, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialize_ReturnsFalseAndLogsError_WhenDirectoryAlreadyExists()
    {
        tempDirectory.Create();
        tempDirectory.Refresh();

        var initializer = new ProjectInitializer();
        string? error = null;
        initializer.OnLogError += logged => error = logged;

        var result = initializer.Initialize(tempDirectory, model);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("already exists", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialize_RaisesLogErrorEvent_WhenDirectoryCannotBeCreated()
    {
        var parent = Path.Combine(Path.GetTempPath(), "Veeling.Tests");
        Directory.CreateDirectory(parent);
        var filePath = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        File.WriteAllText(filePath, "reserved");

        try
        {
            var initializer = new ProjectInitializer();
            string? error = null;
            initializer.OnLogError += logged => error = logged;

            initializer.Initialize(new DirectoryInfo(filePath), model);

            Assert.NotNull(error);
            Assert.Contains("Failed to create project directory", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void Initialize_CreatesGlossaryFilesForNonMasterLanguages()
    {
        ProjectModel glossaryModel = new ProjectModel
        {
            Name = "Test",
            Description = "Test project",
            MasterLanguage = "en",
            Languages = [new Language("en"), new Language("fi")],
            Style = new Style { Tone = Tone.Neutral, Formality = Formality.Neutral }
        };

        var initializer = new ProjectInitializer();

        initializer.Initialize(tempDirectory, glossaryModel);

        string fiGlossaryPath = Path.Combine(tempDirectory.FullName, "glossary.fi.yaml");
        string enGlossaryPath = Path.Combine(tempDirectory.FullName, "glossary.en.yaml");

        Assert.True(File.Exists(fiGlossaryPath));
        Assert.False(File.Exists(enGlossaryPath));
    }
}
