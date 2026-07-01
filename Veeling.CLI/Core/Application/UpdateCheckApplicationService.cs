using Microsoft.Extensions.Options;
using Veeling.CLI.Configuration;

namespace Veeling.Core.Application;

public sealed class UpdateCheckApplicationService(
    IReleaseMetadataClient metadataClient,
    IUpdateCheckCache cache,
    IOptions<UpdateCheckOptions> options)
    : IUpdateCheckService
{
    private readonly IReleaseMetadataClient metadataClient = metadataClient;
    private readonly IUpdateCheckCache cache = cache;
    private readonly UpdateCheckOptions options = options.Value;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UpdateCheckCacheEntry? cached = cache.Read();

        if (cached is not null && IsFresh(cached.CheckedAtUtc, now))
        {
            return new UpdateCheckResult(
                FromCache: true,
                Success: true,
                Metadata: cached.Metadata,
                FailureReason: null,
                CheckedAtUtc: cached.CheckedAtUtc);
        }

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));

        try
        {
            ReleaseMetadata metadata = await metadataClient.FetchAsync(timeoutCts.Token);
            cache.Write(new UpdateCheckCacheEntry(now, metadata));

            return new UpdateCheckResult(
                FromCache: false,
                Success: true,
                Metadata: metadata,
                FailureReason: null,
                CheckedAtUtc: now);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OfflineSafeFallback(now, cached, "timeout");
        }
        catch (Exception ex)
        {
            return OfflineSafeFallback(now, cached, ex.GetType().Name);
        }
    }

    private bool IsFresh(DateTimeOffset checkedAtUtc, DateTimeOffset now)
    {
        int ttlHours = Math.Max(1, options.CacheTtlHours);
        return now - checkedAtUtc < TimeSpan.FromHours(ttlHours);
    }

    private static UpdateCheckResult OfflineSafeFallback(DateTimeOffset now, UpdateCheckCacheEntry? cached, string reason)
    {
        if (cached is not null)
        {
            return new UpdateCheckResult(
                FromCache: true,
                Success: true,
                Metadata: cached.Metadata,
                FailureReason: reason,
                CheckedAtUtc: cached.CheckedAtUtc);
        }

        return new UpdateCheckResult(
            FromCache: false,
            Success: false,
            Metadata: null,
            FailureReason: reason,
            CheckedAtUtc: now);
    }
}
