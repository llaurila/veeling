namespace Veeling.Core.Application;

public interface IUpdateCheckCache
{
    UpdateCheckCacheEntry? Read();

    void Write(UpdateCheckCacheEntry entry);
}

public sealed record UpdateCheckCacheEntry(DateTimeOffset CheckedAtUtc, ReleaseMetadata Metadata);
