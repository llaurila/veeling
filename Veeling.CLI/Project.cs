using System.Text.RegularExpressions;
using Veeling.CLI.Exceptions;
using Veeling.Models;
using Veeling.Models.Schema;

namespace Veeling.CLI;

public class Project
{
    public const string ProjectFileName = "Project.yaml";
    public const string DataDirectoryName = "data";
    public const string SchemaFileExtension = ".schema.yaml";

    public static readonly Regex DataFileRegex = new(@"^(?<name>[^\.]+)\.(?<lang>[a-z]{2})\.yaml$", RegexOptions.Compiled);

    private readonly FileInfo projectFile;

    public Project(string projectFilePath)
        : this(new FileInfo(projectFilePath))
    {
    }

    public Project(FileInfo projectFile)
    {
        if (!projectFile.Exists)
        {
            throw new FileNotFoundException(
                $"The project file '{projectFile.FullName}' does not exist.");
        }

        this.projectFile = projectFile;

        string yamlContent = File.ReadAllText(projectFile.FullName);
        Model = ProjectModel.FromYaml(yamlContent);
    }

    public DirectoryInfo Directory => projectFile.Directory!;

    public ProjectModel Model { get; private set; }

    public void Refresh()
    {
        if (projectFile.Exists)
        {
            string yamlContent = File.ReadAllText(projectFile.FullName);
            Model = ProjectModel.FromYaml(yamlContent);
        }
    }

    public bool SupportsLanguage(Language language)
    {
        return Model.Languages.Contains(language);
    }

    public bool SupportsLanguage(string languageCode)
    {
        if (!Language.IsSupportedLanguage(languageCode))
        {
            return false;
        }

        return Model.Languages.Any(language => language.Code == languageCode);
    }

    public IEnumerable<VSchema> GetSchemas()
    {
        Directory.Refresh();
        return VSchema.ReadAll(Directory);
    }

    public Dictionary<string, DataModel>? GetData(VSchema schema, Language lang)
    {
        FileInfo dataFile = GetDataFileInfo(schema, lang);
        if (!dataFile.Exists) return null;

        string yamlContent = File.ReadAllText(dataFile.FullName);
        DataModel[] records = DataModel.FromYaml(yamlContent);

        try
        {
            schema.Validate(lang, records);
        }
        catch (VeelingSchemaException ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return null;
        }

        return records.ToDictionary(r => r.Name);
    }

    public void SaveData(VSchema schema, Language lang, IEnumerable<DataModel> records)
    {
        DirectoryInfo dataDir = new(
            Path.Combine(
                Directory.FullName,
                DataDirectoryName
            )
        );

        if (!dataDir.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The data directory '{dataDir.FullName}' does not exist."
            );
        }

        FileInfo dataFile = GetDataFileInfo(schema, lang);
        string yamlContent = DataModel.ToYaml([.. records]);
        AtomicFile.WriteAllText(dataFile, yamlContent);
    }

    public IEnumerable<RecordLocator> GetRecordLocators(RecordFilter filter)
    {
        return GetRecordLocators()
            .Where(loc => filter.Matches(loc.Schema, loc.Field, loc.Language));
    }

    public IEnumerable<RecordLocator> GetRecordLocators()
    {
        foreach (VSchema schema in GetSchemas())
        {
            foreach (Language lang in Model.Languages)
            {
                foreach (SchemaFieldModel field in schema.Model.Model)
                {
                    yield return new RecordLocator
                    (
                        Schema: schema.Model.Name,
                        Field: field.Name,
                        Language: lang
                    );
                }
            }
        }
    }

    public VSchema? GetSchema(string schemaName)
    {
        return GetSchemas().SingleOrDefault(s => s.Model.Name == schemaName);
    }

    private FileInfo GetDataFileInfo(VSchema schema, Language lang)
    {
        return new FileInfo(
            Path.Combine(
                Directory.FullName,
                DataDirectoryName,
                $"{schema.Model.Name}.{lang.Code}.yaml"
            )
        );
    }
}
