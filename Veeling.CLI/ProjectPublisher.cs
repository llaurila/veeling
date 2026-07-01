using System.Text.Json;
using System.Text.Json.Nodes;
using Veeling.CLI.Exceptions;
using Veeling.Models;

namespace Veeling.CLI;

public class ProjectPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string Publish(Project project)
    {
        JsonObject root = [];

        foreach (Language lang in project.Model.Languages)
        {
            root[lang.Code] = Publish(project, lang);
        }

        return root.ToJsonString(JsonOptions);
    }

    private JsonObject Publish(Project project, Language lang)
    {
        JsonObject langObj = [];

        DirectoryInfo dataDir = new(
            Path.Combine(
                project.Directory.FullName,
                Project.DataDirectoryName
            )
        );

        if (!dataDir.Exists) return langObj;

        foreach (VSchema schema in project.GetSchemas())
        {
            langObj[schema.Model.Name] = Publish(project, schema, lang);
        }

        return langObj;
    }

    private JsonObject Publish(Project project, VSchema schema, Language lang)
    {
        FileInfo dataFile = GetDataFileInfo(project, schema, lang);

        if (!dataFile.Exists)
        {
            dataFile = GetDataFileInfo(project, schema, project.Model.MasterLanguage);
            if (!dataFile.Exists)
            {
                throw new ProjectPublishException(
                    $"The master language '{project.Model.MasterLanguage}' does not exist for '{schema.Model.Name}'."
                );
            }
        }

        string yamlContent = File.ReadAllText(dataFile.FullName);
        DataModel[] records = DataModel.FromYaml(yamlContent);

        try
        {
            schema.Validate(lang, records);
        }
        catch (VeelingSchemaException ex)
        {
            throw new ProjectPublishException($"Error: {ex.Message}", ex);
        }

        JsonObject recordsObj = [];

        foreach (DataModel record in records)
        {
            recordsObj[record.Name] = record.Value;
        }

        return recordsObj;
    }

    private static FileInfo GetDataFileInfo(Project project, VSchema schema, Language lang)
    {
        return new FileInfo(
            Path.Combine(
                project.Directory.FullName,
                Project.DataDirectoryName,
                $"{schema.Model.Name}.{lang.Code}.yaml"
            )
        );
    }
}
