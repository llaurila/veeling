using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.Core.Application;
using Veeling.Models;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Veeling.CLI.Tests;

public static class CliTestHost
{
    public static ServiceProvider CreateServiceProvider(
        IDictionary<string, string?>? settings = null,
        FileInfo? globalConfigFile = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        ServiceCollection services = new();
        services.AddVeelingCli(configuration);
        services.AddSingleton<IUpdateCheckCache, InMemoryUpdateCheckCache>();

        if (globalConfigFile is not null)
        {
            services.AddSingleton<IGlobalConfigFileLocator>(new FixedGlobalConfigFileLocator(globalConfigFile));
        }

        return services.BuildServiceProvider();
    }

    private sealed class FixedGlobalConfigFileLocator(FileInfo file) : IGlobalConfigFileLocator
    {
        public FileInfo GetGlobalConfigFile() => file;
    }

    private sealed class InMemoryUpdateCheckCache : IUpdateCheckCache
    {
        private UpdateCheckCacheEntry? entry;

        public UpdateCheckCacheEntry? Read() => entry;

        public void Write(UpdateCheckCacheEntry entry)
        {
            this.entry = entry;
        }
    }
}

public sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter originalOut = Console.Out;
    private readonly TextWriter originalError = Console.Error;
    private readonly TextReader originalIn = Console.In;

    public ConsoleCapture(string input = "")
    {
        StdOut = new StringWriter();
        StdErr = new StringWriter();

        Console.SetOut(StdOut);
        Console.SetError(StdErr);
        Console.SetIn(new StringReader(input));
    }

    public StringWriter StdOut { get; }

    public StringWriter StdErr { get; }

    public void Dispose()
    {
        Console.SetOut(originalOut);
        Console.SetError(originalError);
        Console.SetIn(originalIn);
        StdOut.Dispose();
        StdErr.Dispose();
    }
}

public sealed class CurrentDirectoryScope : IDisposable
{
    private readonly string originalDirectory = Directory.GetCurrentDirectory();

    public CurrentDirectoryScope(string directory)
    {
        Directory.SetCurrentDirectory(directory);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(originalDirectory);
    }
}

public static class TestProjectFactory
{
    public static FileInfo CreateProjectFile(DirectoryInfo projectDir, string[]? languages = null, string masterLanguage = "en")
    {
        languages ??= [masterLanguage];
        Directory.CreateDirectory(projectDir.FullName);

        string languageYaml = string.Join(Environment.NewLine, languages.Select(lang => $"  - {lang}"));

        string projectYaml = $@"
name: TestProject

description: Test project

languages:
{languageYaml}

master_language: {masterLanguage}

style:
  tone: neutral
  formality: neutral
  audience: general
";

        string projectFilePath = Path.Combine(projectDir.FullName, Project.ProjectFileName);
        File.WriteAllText(projectFilePath, projectYaml.TrimStart());
        return new FileInfo(projectFilePath);
    }

    public static FileInfo CreateSchemaFile(DirectoryInfo projectDir, string schemaName = "Schema1", params string[] fieldNames)
    {
        if (fieldNames.Length == 0)
        {
            fieldNames = ["Field1"];
        }

        string modelYaml = string.Join(
            Environment.NewLine + Environment.NewLine,
            fieldNames.Select(field => $"  - name: {field}{Environment.NewLine}    description: {field} description."));

        string schemaYaml = $@"
name: {schemaName}

description: Example schema

model:
{modelYaml}
";

        string schemaFilePath = Path.Combine(projectDir.FullName, $"{schemaName}.schema.yaml");
        File.WriteAllText(schemaFilePath, schemaYaml.TrimStart());
        return new FileInfo(schemaFilePath);
    }

    public static DirectoryInfo EnsureDataDirectory(DirectoryInfo projectDir)
    {
        string dataDirectory = Path.Combine(projectDir.FullName, Project.DataDirectoryName);
        return Directory.CreateDirectory(dataDirectory);
    }

    public static FileInfo CreateDataFile(DirectoryInfo projectDir, string schemaName, string languageCode, params DataModel[] records)
    {
        DirectoryInfo dataDirectory = EnsureDataDirectory(projectDir);
        string dataFilePath = Path.Combine(dataDirectory.FullName, $"{schemaName}.{languageCode}.yaml");
        File.WriteAllText(dataFilePath, DataModel.ToYaml(records));
        return new FileInfo(dataFilePath);
    }
}
