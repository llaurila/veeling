namespace Veeling.Core.Application;

public sealed record ReleaseMetadata(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    ReleaseMetadataSource Source,
    ReleaseMetadataChannels Channels);

public sealed record ReleaseMetadataSource(string Repository, string ReleasesUrl);

public sealed record ReleaseMetadataChannels(ReleaseChannel Stable, ReleaseChannel? Prerelease);

public sealed record ReleaseChannel(
    string Version,
    string Tag,
    DateTimeOffset PublishedAt,
    string ReleaseUrl,
    string NotesUrl,
    string ChangelogUrl,
    ReleaseCompatibility Compatibility);

public sealed record ReleaseCompatibility(string? MinimumCliVersion, string Notes);

public sealed record UpdateCheckResult(
    bool FromCache,
    bool Success,
    ReleaseMetadata? Metadata,
    string? FailureReason,
    DateTimeOffset CheckedAtUtc);
