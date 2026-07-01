using Veeling.CLI.Exceptions;
using Veeling.Models;

namespace Veeling.CLI.Providers;

/* Key: data file ID (format: "schema:language")
 * Value: dictionary of records in that data file (keyed by field name) */
using CachedData = Dictionary<
    string,
    Dictionary<string, DataModel>
>;

public sealed class FileSystemProjectDataSessionFactory : IProjectDataSessionFactory
{
    public IProjectDataSession Open(Project project)
    {
        return new FileSystemProjectDataSession(project);
    }
}

public sealed class FileSystemProjectDataSession(Project project) : IProjectDataSession
{
    private readonly HashSet<DataFileLocator> changedDataFiles = [];
    private readonly CachedData data = [];

    public Project Project { get; } = project ?? throw new ArgumentNullException(nameof(project));

    public IEnumerable<DataRetrieveResult> Get(RecordFilter recordSpec)
    {
        EnsureData(recordSpec);

        foreach (RecordLocator rl in Project.GetRecordLocators(recordSpec))
        {
            string id = GetDataFileId(rl);

            data[id].TryGetValue(rl.Field, out DataModel? record);

            yield return new DataRetrieveResult(
                DataModel: record,
                RecordLocator: rl
            );
        }
    }

    public void Set(RecordLocator recordLocator, DataModel record)
    {
        if (!recordLocator.Field.Equals(record.Name))
        {
            throw new ArgumentException(
                $"Field name '{record.Name}' doesn't match the record spec {recordLocator}.",
                nameof(recordLocator)
            );
        }

        EnsureData(
            new DataFileLocator(
                Schema: recordLocator.Schema,
                Language: recordLocator.Language
            )
        );

        DataFileLocator dfl = new(recordLocator.Schema.ToString(), recordLocator.Language.ToString());

        string id = GetDataFileId(dfl);

        Dictionary<string, DataModel> records = data[id];

        records[record.Name] = record;

        changedDataFiles.Add(dfl);
    }

    public bool HasPendingChanges => changedDataFiles.Count != 0;

    public void SaveChanges()
    {
        List<DataFileLocator> changedFiles = [.. changedDataFiles.OrderBy(x => x.Schema).ThenBy(x => x.Language.Code)];

        foreach (DataFileLocator dfl in changedFiles)
        {
            FileInfo dataFile = GetDataFileInfo(dfl.Schema, dfl.Language.Code);

            Dictionary<string, DataModel> records = data[GetDataFileId(dfl)];
            string yaml = DataModel.ToYaml([.. records.Values]);

            try
            {
                AtomicFile.WriteAllText(dataFile, yaml);
            }
            catch (Exception ex)
            {
                throw new PersistenceException(
                    $"Failed to save data file '{dataFile.FullName}' atomically.",
                    ex
                );
            }
        }

        changedDataFiles.Clear();
    }

    public void DiscardChanges()
    {
        changedDataFiles.Clear();
        data.Clear();
    }

    private void EnsureData(RecordFilter recordSpec)
    {
        foreach (string schemaName in GetSchemaNames(recordSpec))
        {
            foreach (Language language in GetLanguages(recordSpec))
            {
                EnsureData(new DataFileLocator(
                    Schema: schemaName,
                    Language: language
                ));
            }
        }
    }

    private void EnsureData(DataFileLocator dfl)
    {
        string id = GetDataFileId(dfl.Schema, dfl.Language.Code);
        if (data.ContainsKey(id)) return;

        RequireSupportedLanguage(dfl.Language.Code);
        RequireSchema(dfl.Schema);

        Dictionary<string, DataModel> records = [];

        FileInfo dataFile = GetDataFileInfo(dfl.Schema, dfl.Language.Code);

        if (dataFile.Exists)
        {
            records = DataModel.FromYaml(File.ReadAllText(dataFile.FullName)).ToDictionary(r => r.Name);
        }

        data[id] = records;
    }

    private IEnumerable<Language> GetLanguages(RecordFilter recordSpec)
    {
        if (recordSpec.Language.IsAny)
        {
            return Project.Model.Languages;
        }

        return [(Language)recordSpec.Language.ToString()];
    }

    private void RequireSchema(string schemaName)
    {
        FileInfo schemaFile = GetSchemaFileInfo(schemaName);
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException(
                $"Schema file for schema '{schemaName}' not found.",
                schemaFile.FullName
            );
        }
    }

    private FileInfo GetSchemaFileInfo(string schemaName)
    {
        return new FileInfo(
            Path.Combine(Project.Directory.FullName,
            $"{schemaName}.schema.yaml")
        );
    }

    private FileInfo GetDataFileInfo(string schemaName, string languageCode)
    {
        return new FileInfo(
            Path.Combine(Project.Directory.FullName,
            "data",
            $"{schemaName}.{languageCode}.yaml")
        );
    }

    private void RequireSupportedLanguage(string languageCode)
    {
        if (!Project.SupportsLanguage(languageCode))
        {
            throw new ArgumentException(
                $"Language '{languageCode}' is not supported by the project.",
                nameof(languageCode)
            );
        }
    }

    private static string GetDataFileId(DataFileLocator dfl)
    {
        return GetDataFileId(dfl.Schema, dfl.Language.Code);
    }

    private static string GetDataFileId(RecordLocator rl)
    {
        return GetDataFileId(rl.Schema, rl.Language.Code);
    }

    private static string GetDataFileId(string schemaName, string languageCode)
    {
        return $"{schemaName}:{languageCode}";
    }

    private IEnumerable<string> GetSchemaNames(RecordFilter recordSpec)
    {
        return GetSchemaNames().Where(s => recordSpec.Schema.IsMatch(s));
    }

    private IEnumerable<string> GetSchemaNames()
    {
        return VSchema.GetSchemaFiles(Project.Directory)
            .Select(f => f.Name.Replace(Project.SchemaFileExtension, ""));
    }
}
