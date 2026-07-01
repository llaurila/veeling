namespace Veeling.Models;

public enum DataStatus
{
    Unknown,
    New,
    NeedsReview,
    Approved,
    Bad
}

public class DataMetaModel
{
    public int? Version { get; set; }

    public string? Comment { get; set; }

    public DataStatus Status { get; set; } = DataStatus.Unknown;

    public DateTime? LastUpdate { get; set; }

    public string? UpdatedBy { get; set; }

    public string? Hash { get; set; }

    public string? SourceHash { get; set; }

    public bool NeedsApproval() =>
        Status == DataStatus.New ||
        Status == DataStatus.NeedsReview ||
        Status == DataStatus.Bad;

    public void UpdateHash(RecordLocator rl, string content)
    {
        UpdateHash(rl.Language, rl.Field, content);
    }

    public void UpdateHash(Language language, string fieldName, string content)
    {
        Hash = GetHash(language, fieldName, content);
    }

    public void UpdateSourceHash(RecordLocator rl, string sourceContent)
    {
        UpdateSourceHash(rl.Language, rl.Field, sourceContent);
    }

    public void UpdateSourceHash(Language sourceLanguage, string fieldName, string sourceContent)
    {
        SourceHash = GetHash(sourceLanguage, fieldName, sourceContent);
    }

    public bool IsSourceChanged(RecordLocator rl, string sourceContent)
    {
        return IsSourceChanged(rl.Language, rl.Field, sourceContent);
    }

    public bool IsSourceChanged(Language sourceLanguage, string fieldName, string sourceContent)
    {
        string newHash = GetHash(sourceLanguage, fieldName, sourceContent);
        return SourceHash != newHash;
    }

    private static string GetHash(Language lang, string fieldName, string content)
    {
        string normalizedContent = NormalizeLineEndings(content);
        string data = $"{lang.Code}|{fieldName}|{normalizedContent}";
        ulong hash = HashUtil.ComputeFnv1a64(data);
        return HashUtil.ToBase36(hash);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    public void Tick(string? updatedBy = null)
    {
        LastUpdate = DateTime.Now;
        if (updatedBy is not null) UpdatedBy = updatedBy;

        if (Version is null) Version = 1;
        else Version += 1;
    }
}
