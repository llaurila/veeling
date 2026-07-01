using YamlDotNet.Serialization;

namespace Veeling.Models.Schema;

public class SchemaModel
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required List<SchemaFieldModel> Model { get; init; }

    public string ToYaml()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();
        return serializer.Serialize(this);
    }

    public static SchemaModel FromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .WithEnforceRequiredMembers()
            .Build();

        return deserializer.Deserialize<SchemaModel>(yaml);
    }
}
