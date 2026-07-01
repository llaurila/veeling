using System.Text.Json;

namespace Veeling.Core.Application;

public sealed class FileSystemUpdateCheckCache : IUpdateCheckCache
{
    private const string CacheDirectoryName = ".veeling";
    private const string CacheFileName = "update-check-cache.json";

    public UpdateCheckCacheEntry? Read()
    {
        FileInfo file = GetCacheFile();
        if (!file.Exists)
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(file.FullName);
            CacheDocument? dto = JsonSerializer.Deserialize<CacheDocument>(json);
            if (dto is null || dto.Metadata is null)
            {
                return null;
            }

            return new UpdateCheckCacheEntry(dto.CheckedAtUtc, dto.Metadata);
        }
        catch
        {
            return null;
        }
    }

    public void Write(UpdateCheckCacheEntry entry)
    {
        try
        {
            FileInfo file = GetCacheFile();
            Directory.CreateDirectory(file.DirectoryName!);

            string json = JsonSerializer.Serialize(new CacheDocument
            {
                CheckedAtUtc = entry.CheckedAtUtc,
                Metadata = entry.Metadata
            }, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(file.FullName, json);
        }
        catch
        {
            // Non-fatal by design: cache persistence failures must not impact primary flow.
        }
    }

    private static FileInfo GetCacheFile()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string path = Path.Combine(home, CacheDirectoryName, CacheFileName);
        return new FileInfo(path);
    }

    private sealed class CacheDocument
    {
        public DateTimeOffset CheckedAtUtc { get; init; }

        public ReleaseMetadata? Metadata { get; init; }
    }
}
