using System.Text.Json;
using Veeling.CLI;
using Veeling.CLI.Providers;
using Veeling.Models;
using YamlDotNet.Serialization;

namespace Veeling.Core.Application;

public enum ExportOutputFormat
{
    Yaml,
    Json
}

public sealed class ExportApplicationService(IProjectDataSessionFactory sessionFactory)
{
    public string Execute(Project project, RecordFilter recordSpec, ExportOutputFormat format)
    {
        IProjectDataSession session = sessionFactory.Open(project);
        DataRetrieveResult[] results = [.. session.Get(recordSpec)];

        Dictionary<string, string?> export = results.ToDictionary(
            drr => drr.RecordLocator.ToString(),
            drr => drr.DataModel?.Value);

        return format switch
        {
            ExportOutputFormat.Yaml => SerializeYaml(export),
            ExportOutputFormat.Json => SerializeJson(export),
            _ => throw new InvalidOperationException("Unsupported export format.")
        };
    }

    private static string SerializeYaml(object data)
    {
        ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();

        return serializer.Serialize(data);
    }

    private static string SerializeJson(object data)
    {
        return JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
        );
    }
}
