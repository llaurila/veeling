using Veeling.Models;
using Veeling.Models.Schema;

namespace Veeling.CLI;

public class ProjectInitializer
{
    public event Action<string>? OnLog, OnLogError;

    private DirectoryInfo? di;
    private ProjectModel? model;

    public bool Initialize(DirectoryInfo di, ProjectModel model)
    {
        di.Refresh();
        if (di.Exists)
        {
            LogError($"Directory '{di.FullName}' already exists.");
            return false;
        }

        this.di = di;

        bool flowControl = CreateDirectory(di);
        if (!flowControl) return false;

        Log($"Scaffolding project at {di.FullName}");

        this.model = model;

        Scaffold();

        return true;
    }

    private bool CreateDirectory(DirectoryInfo di)
    {
        try
        {
            di.Create();
        }
        catch (IOException ex)
        {
            LogError($"Failed to create project directory: {ex.Message}");
            return false;
        }

        di.Refresh();
        return true;
    }

    private void Scaffold()
    {
        CreateProjectFile();
        CreateExampleSchema();
        CreateDataDirectory();
        CreateGlossaryFiles();
    }

    private void CreateProjectFile()
    {
        FileInfo fi = new(Path.Combine(di!.FullName, Project.ProjectFileName));
        string yamlContent = model!.ToYaml();
        AtomicFile.WriteAllText(fi, yamlContent);
    }

    private void CreateExampleSchema()
    {
        SchemaModel schema = new()
        {
            Name = "Schema1",
            Description = "Sample schema.",
            Model =
            [
                new() {
                    Name = "Field1",
                    Description = "<describe the field here>"
                }
            ]
        };

        string yamlContent = schema.ToYaml();
        FileInfo fi = new(Path.Combine(di!.FullName, schema.Name + Project.SchemaFileExtension));
        AtomicFile.WriteAllText(fi, yamlContent);
    }

    private void CreateDataDirectory()
    {
        DirectoryInfo dataDi = new(Path.Combine(di!.FullName, Project.DataDirectoryName));
        try
        {
            dataDi.Create();
        }
        catch (IOException ex)
        {
            LogError($"Failed to create data directory: {ex.Message}");
        }
    }

    private void CreateGlossaryFiles()
    {
        Language masterLanguage = model!.MasterLanguage;

        foreach (Language language in model.Languages)
        {
            if (language.Equals(masterLanguage))
            {
                continue;
            }

            GlossaryModel glossary = new()
            {
                Language = language,
                Entries = []
            };

            string fileName = GlossaryLoader.GetGlossaryFileName(language);
            FileInfo file = new(Path.Combine(di!.FullName, fileName));
            AtomicFile.WriteAllText(file, glossary.ToYaml());
        }
    }

    protected void Log(string message)
    {
        OnLog?.Invoke(message);
    }

    protected void LogError(string message)
    {
        OnLogError?.Invoke(message);
    }
}
