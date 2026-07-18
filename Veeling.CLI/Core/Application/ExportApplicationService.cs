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

public sealed record ExportCommandRequest(
    Project Project,
    string? Selector,
    ExportOutputFormat Format
);

public sealed class ExportApplicationService(IProjectDataSessionFactory sessionFactory)
{
    public string Execute(ExportCommandRequest request)
    {
        RecordFilter recordSpec = ResolveSelector(request.Selector);

        IProjectDataSession session = sessionFactory.Open(request.Project);
        DataRetrieveResult[] results = [.. session.Get(recordSpec)];

        Dictionary<string, string?> export = results.ToDictionary(
            drr => drr.RecordLocator.ToString(),
            drr => drr.DataModel?.Value);

        return request.Format switch
        {
            ExportOutputFormat.Yaml => SerializeYaml(export),
            ExportOutputFormat.Json => SerializeJson(export),
            _ => throw new InvalidOperationException("Unsupported export format.")
        };
    }

    private static RecordFilter ResolveSelector(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return RecordFilter.Parse("*.*:*");
        }

        return RecordFilter.Parse(selector);
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
