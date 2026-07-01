using YamlDotNet.Serialization;

namespace Veeling.Models;

public enum GlossaryEntryStatus
{
    Approved,
    Preferred,
    Deprecated,
    Forbidden
}

public enum GlossaryAppliesTo
{
    Ui,
    System,
    Ai
}

public class GlossaryEntryModel
{
    private static readonly GlossaryAppliesTo[] DefaultAppliesTo =
    [
        GlossaryAppliesTo.Ui,
        GlossaryAppliesTo.System,
        GlossaryAppliesTo.Ai
    ];

    public required string Term { get; init; }

    public required string Translation { get; init; }

    public required GlossaryEntryStatus Status { get; init; }

    public string? Note { get; init; }

    public string[] ForbiddenVariants { get; init; } = [];

    public GlossaryAppliesTo[] AppliesTo { get; init; } = [.. DefaultAppliesTo];
}

public class GlossaryModel
{
    public required Language Language { get; init; }

    public required GlossaryEntryModel[] Entries { get; init; }

    public static GlossaryModel FromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .WithEnforceRequiredMembers()
            .Build();

        return deserializer.Deserialize<GlossaryModel>(yaml);
    }

    public string ToYaml()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();

        return serializer.Serialize(this);
    }
}
