namespace Veeling.Core.Application;

public sealed class UpdateAdvisoryApplicationService(
    IUpdateCheckService updateCheckApplicationService)
{
    private readonly IUpdateCheckService updateCheckApplicationService = updateCheckApplicationService;

    public async Task<UpdateAdvisoryResult> BuildAdvisoryAsync(
        string currentVersion,
        UpdateAdvisoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Enabled)
        {
            return new UpdateAdvisoryResult(false, null, null, false, false, request.Manual, null, null);
        }

        UpdateCheckResult check = await updateCheckApplicationService.CheckForUpdatesAsync(cancellationToken);
        if (!check.Success || check.Metadata is null)
        {
            return new UpdateAdvisoryResult(false, null, null, true, false, request.Manual, check.FailureReason, null);
        }

        ReleaseChannel channel = request.IncludePrerelease && check.Metadata.Channels.Prerelease is not null
            ? check.Metadata.Channels.Prerelease
            : check.Metadata.Channels.Stable;

        if (!TryParseVersion(channel.Version, out Version? latest)
            || !TryParseVersion(currentVersion, out Version? current)
            || latest <= current)
        {
            return new UpdateAdvisoryResult(false, channel.Version, channel.ReleaseUrl, false, check.FromCache, request.Manual, null, null);
        }

        string guidance = BuildUpdateGuidance(channel.Version, request.InstallSourceHint);
        return new UpdateAdvisoryResult(
            UpdateAvailable: true,
            LatestVersion: channel.Version,
            ReleaseUrl: channel.ReleaseUrl,
            CheckFailed: false,
            FromCache: check.FromCache,
            Manual: request.Manual,
            FailureReason: null,
            Guidance: guidance);
    }

    public string BuildSelfUpdateGuidance(string? installSourceHint)
    {
        return BuildUpdateGuidance(null, installSourceHint);
    }

    private string BuildUpdateGuidance(string? latestVersion, string? installSourceHint)
    {
        string source = (installSourceHint ?? string.Empty).Trim().ToLowerInvariant();
        string suffix = latestVersion is null ? string.Empty : $" (latest: {latestVersion})";

        if (source is "nuget" or "dotnet-tool" or "dotnet")
        {
            return $"Update via NuGet global tool{suffix}: dotnet tool update --global veeling";
        }

        if (source is "archive" or "zip" or "tar")
        {
            return $"Update via release archive{suffix}: download the new archive for your RID, verify SHA256SUMS, replace existing install directory, then run veeling --version.";
        }

        return $"Install source not detected. Safe update options{suffix}:\n"
            + "  1) NuGet global tool: dotnet tool update --global veeling\n"
            + "  2) Archive install: download the latest archive for your RID, verify SHA256SUMS, replace existing install directory.";
    }

    private static bool TryParseVersion(string value, out Version? version)
    {
        string core = value.Split('-', 2)[0];
        return Version.TryParse(core, out version);
    }
}

public sealed record UpdateAdvisoryRequest(
    bool Enabled,
    bool Manual,
    bool IncludePrerelease,
    string? InstallSourceHint);

public sealed record UpdateAdvisoryResult(
    bool UpdateAvailable,
    string? LatestVersion,
    string? ReleaseUrl,
    bool CheckFailed,
    bool FromCache,
    bool Manual,
    string? FailureReason,
    string? Guidance);
