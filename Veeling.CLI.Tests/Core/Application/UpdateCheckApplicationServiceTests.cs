using Microsoft.Extensions.Options;
using Veeling.CLI.Configuration;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class UpdateCheckApplicationServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_NoFreshCache_FetchesAndWritesCache()
    {
        TestCache cache = new();
        TestMetadataClient client = new(TestData.Metadata);
        UpdateCheckApplicationService service = CreateService(client, cache, ttlHours: 24, timeoutSeconds: 2);

        UpdateCheckResult result = await service.CheckForUpdatesAsync();

        Assert.True(result.Success);
        Assert.False(result.FromCache);
        Assert.NotNull(result.Metadata);
        Assert.NotNull(cache.LastWritten);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_FreshCache_ReturnsCacheWithoutNetworkCall()
    {
        TestCache cache = new
        (
            new UpdateCheckCacheEntry(DateTimeOffset.UtcNow.AddHours(-1), TestData.Metadata)
        );

        TestMetadataClient client = new(TestData.Metadata);
        UpdateCheckApplicationService service = CreateService(client, cache, ttlHours: 24, timeoutSeconds: 2);

        UpdateCheckResult result = await service.CheckForUpdatesAsync();

        Assert.True(result.Success);
        Assert.True(result.FromCache);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_TimeoutWithStaleCache_FallsBackToCache()
    {
        TestCache cache = new
        (
            new UpdateCheckCacheEntry(DateTimeOffset.UtcNow.AddHours(-48), TestData.Metadata)
        );

        TimeoutMetadataClient client = new();
        UpdateCheckApplicationService service = CreateService(client, cache, ttlHours: 24, timeoutSeconds: 1);

        UpdateCheckResult result = await service.CheckForUpdatesAsync();

        Assert.True(result.Success);
        Assert.True(result.FromCache);
        Assert.Equal("timeout", result.FailureReason);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NetworkFailureNoCache_ReturnsNonFatalFailure()
    {
        TestCache cache = new();
        ThrowingMetadataClient client = new(new HttpRequestException("offline"));
        UpdateCheckApplicationService service = CreateService(client, cache, ttlHours: 24, timeoutSeconds: 2);

        UpdateCheckResult result = await service.CheckForUpdatesAsync();

        Assert.False(result.Success);
        Assert.False(result.FromCache);
        Assert.Null(result.Metadata);
        Assert.Equal(nameof(HttpRequestException), result.FailureReason);
    }

    private static UpdateCheckApplicationService CreateService(
        IReleaseMetadataClient client,
        IUpdateCheckCache cache,
        int ttlHours,
        int timeoutSeconds)
    {
        return new UpdateCheckApplicationService(
            client,
            cache,
            Options.Create(new UpdateCheckOptions
            {
                CacheTtlHours = ttlHours,
                TimeoutSeconds = timeoutSeconds,
                MetadataUrl = "https://example.invalid/latest.json"
            }));
    }

    private static class TestData
    {
        public static readonly ReleaseMetadata Metadata = new(
            SchemaVersion: "1.0",
            GeneratedAt: DateTimeOffset.UtcNow,
            Source: new ReleaseMetadataSource("https://example.com/repo", "https://example.com/releases"),
            Channels: new ReleaseMetadataChannels(
                Stable: new ReleaseChannel(
                    Version: "0.4.1",
                    Tag: "v0.4.1",
                    PublishedAt: DateTimeOffset.UtcNow,
                    ReleaseUrl: "https://example.com/releases/v0.4.1",
                    NotesUrl: "https://example.com/releases/v0.4.1",
                    ChangelogUrl: "https://example.com/changelog",
                    Compatibility: new ReleaseCompatibility(null, "none")),
                Prerelease: null));
    }

    private sealed class TestCache(UpdateCheckCacheEntry? entry = null) : IUpdateCheckCache
    {
        private UpdateCheckCacheEntry? entry = entry;

        public UpdateCheckCacheEntry? LastWritten { get; private set; }

        public UpdateCheckCacheEntry? Read() => entry;

        public void Write(UpdateCheckCacheEntry entry)
        {
            this.entry = entry;
            LastWritten = entry;
        }
    }

    private sealed class TestMetadataClient(ReleaseMetadata metadata) : IReleaseMetadataClient
    {
        public int CallCount { get; private set; }

        public Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(metadata);
        }
    }

    private sealed class ThrowingMetadataClient(Exception exception) : IReleaseMetadataClient
    {
        public Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class TimeoutMetadataClient : IReleaseMetadataClient
    {
        public async Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return TestData.Metadata;
        }
    }
}
