
namespace Veeling.Models;

public record class RecordLocator(string Schema, string Field, Language Language)
{
    public RecordLocator InLanguage(Language language)
    {
        return new RecordLocator(Schema, Field, language);
    }

    public RecordFilter AsFilter() => RecordFilter.Parse($"{Schema}.{Field}:{Language}");

    public override string ToString() => $"{Schema}.{Field}:{Language}";

    public static RecordLocator Parse(string s)
    {
        string[] parts = s.Split(':');
        if (parts.Length != 2)
            throw new FormatException($"Invalid record locator format: {s}");

        string[] schemaField = parts[0].Split('.');
        if (schemaField.Length != 2)
            throw new FormatException($"Invalid record locator format: {s}");

        return new RecordLocator(
            Schema: schemaField[0],
            Field: schemaField[1],
            Language: parts[1]
        );
    }
}