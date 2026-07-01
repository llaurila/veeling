using YamlDotNet.Serialization;

namespace Veeling.Models;

public class ProjectModel
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required Language MasterLanguage { get; init; }

    public required Language[] Languages { get; init; }

    public required Style Style { get; init; }

    public static ProjectModel FromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .WithEnforceRequiredMembers()
            .Build();

        return deserializer.Deserialize<ProjectModel>(yaml);
    }

    public string ToYaml()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();
        return serializer.Serialize(this);
    }
}
