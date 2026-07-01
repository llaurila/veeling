using System.Text.RegularExpressions;
using Veeling.CLI.Exceptions;
using Veeling.Models;
using Veeling.Models.Schema;

namespace Veeling.CLI;

public class VSchema
{
    public static readonly Regex SchemaFilenameRegex = new(@"^(?<name>[^\.]+)\.schema\.yaml$", RegexOptions.Compiled);

    public static readonly Regex IdentifierRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]{1,50}$", RegexOptions.Compiled);

    public const string IdentifierPatternDescription = "1-50 characters; ASCII letters, numbers, spaces, underscores, and hyphens are allowed.";

    public static readonly Regex IdentifierWildcardRegex = new(@"^[a-zA-Z0-9_\-\*\?]{1,50}$", RegexOptions.Compiled);

    private FileInfo schemaFile;

    public VSchema(string schemaFilePath)
        : this(new FileInfo(schemaFilePath))
    {
    }

    public VSchema(FileInfo schemaFile)
    {
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException(
                $"The schema file '{schemaFile.FullName}' does not exist.");
        }

        this.schemaFile = schemaFile;

        string yamlContent = File.ReadAllText(schemaFile.FullName);
        Model = SchemaModel.FromYaml(yamlContent);

        ValidateName();
    }

    private void ValidateName()
    {
        if (!IdentifierRegex.IsMatch(Model.Name))
        {
            throw new VeelingSchemaException(
                $"The schema name '{Model.Name}' in file '{schemaFile.FullName}' is not a valid identifier.");
        }
    }

    public SchemaModel Model { get; private set; }

    public IEnumerable<SchemaFieldModel> GetFields()
    {
        return Model.Model;
    }

    public void Validate(Language lang, DataModel[] data)
    {
        foreach (SchemaFieldModel field in Model.Model)
        {
            bool fieldExists = data.Any(d => d.Name == field.Name);

            if (!fieldExists)
            {
                throw new VeelingSchemaException(
                    $"The required field '{field.Name}' is missing in '{Model.Name}' ({lang}).");
            }
        }
    }

    public static IEnumerable<VSchema> ReadAll(DirectoryInfo projectDir)
    {
        foreach (FileInfo schemaFile in GetSchemaFiles(projectDir))
        {
            yield return new VSchema(schemaFile);
        }
    }

    public static IEnumerable<FileInfo> GetSchemaFiles(DirectoryInfo projectDir)
    {
        FileInfo[] schemaFiles = projectDir.GetFiles();
        if (schemaFiles.Length == 0) yield break;

        foreach (FileInfo schemaFile in schemaFiles)
        {
            if (SchemaFilenameRegex.IsMatch(schemaFile.Name))
            {
                yield return schemaFile;
            }
        }
    }
}
