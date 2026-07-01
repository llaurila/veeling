namespace Veeling.CLI.Configuration;

public sealed class UpdateCheckOptions
{
    public string MetadataUrl { get; set; } = "https://raw.githubusercontent.com/llaurila/veeling/main/release-metadata/latest.json";

    public int TimeoutSeconds { get; set; } = 2;

    public int CacheTtlHours { get; set; } = 24;
}
