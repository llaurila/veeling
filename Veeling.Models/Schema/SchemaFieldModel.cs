namespace Veeling.Models.Schema;

public class SchemaFieldModel
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool? Multiline { get; init; }
}
