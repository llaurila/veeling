using Veeling.CLI.Exceptions;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class GlossaryLoaderTests
{
    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.GlossaryTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            Project project = new(projectFile.FullName);

            GlossaryLoader loader = new();
            GlossaryModel? glossary = loader.Load(project, new Language("fi"));

            Assert.Null(glossary);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void Load_FileLanguageMismatch_ThrowsGlossaryValidationException()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.GlossaryTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            Project project = new(projectFile.FullName);

            string glossaryPath = Path.Combine(projectDirectory.FullName, "glossary.fi.yaml");
            File.WriteAllText(glossaryPath, @"
language: en

entries:
  - term: Sign in
    translation: Kirjaudu sisaan
    status: approved
");

            GlossaryLoader loader = new();

            GlossaryValidationException ex = Assert.Throws<GlossaryValidationException>(
                () => loader.Load(project, new Language("fi"))
            );

            Assert.Contains("targets language 'fi'", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("contains language 'en'", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void Load_ValidGlossary_ReturnsGlossary()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "Veeling.GlossaryTests", Guid.NewGuid().ToString("N"));
        DirectoryInfo projectDirectory = Directory.CreateDirectory(rootPath);

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory, ["en", "fi"], "en");
            Project project = new(projectFile.FullName);

            string glossaryPath = Path.Combine(projectDirectory.FullName, "glossary.fi.yaml");
            File.WriteAllText(glossaryPath, @"
language: fi

entries:
  - term: Account
    translation: Tili
    status: preferred
");

            GlossaryLoader loader = new();
            GlossaryModel? glossary = loader.Load(project, new Language("fi"));

            Assert.NotNull(glossary);
            GlossaryEntryModel entry = Assert.Single(glossary!.Entries);
            Assert.Equal("Account", entry.Term);
            Assert.Equal("Tili", entry.Translation);
            Assert.Equal(
                [GlossaryAppliesTo.Ui, GlossaryAppliesTo.System, GlossaryAppliesTo.Ai],
                entry.AppliesTo
            );
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }
}
