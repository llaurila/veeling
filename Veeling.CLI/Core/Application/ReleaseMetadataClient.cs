using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Veeling.CLI.Configuration;

namespace Veeling.Core.Application;

public sealed class ReleaseMetadataClient(
    HttpClient httpClient,
    IOptions<UpdateCheckOptions> options,
    ILogger<ReleaseMetadataClient> logger) : IReleaseMetadataClient
{
    private readonly HttpClient httpClient = httpClient;
    private readonly UpdateCheckOptions options = options.Value;
    private readonly ILogger<ReleaseMetadataClient> logger = logger;

    public async Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Fetching release metadata from {MetadataUrl}.", options.MetadataUrl);
        using HttpResponseMessage response = await httpClient.GetAsync(options.MetadataUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        ReleaseMetadataDocument? dto = await response.Content.ReadFromJsonAsync<ReleaseMetadataDocument>(cancellationToken: cancellationToken);
        if (dto is null)
        {
            throw new InvalidOperationException("Release metadata payload was empty.");
        }

        return dto.ToModel();
    }

    private sealed class ReleaseMetadataDocument
    {
        public string? schema_version { get; init; }
        public DateTimeOffset generated_at { get; init; }
        public SourceDocument? source { get; init; }
        public ChannelsDocument? channels { get; init; }

        public ReleaseMetadata ToModel()
        {
            if (string.IsNullOrWhiteSpace(schema_version))
            {
                throw new InvalidOperationException("Release metadata schema_version is required.");
            }

            if (source is null)
            {
                throw new InvalidOperationException("Release metadata source is required.");
            }

            if (channels?.stable is null)
            {
                throw new InvalidOperationException("Release metadata channels.stable is required.");
            }

            return new ReleaseMetadata(
                SchemaVersion: schema_version,
                GeneratedAt: generated_at,
                Source: source.ToModel(),
                Channels: channels.ToModel());
        }
    }

    private sealed class SourceDocument
    {
        public string? repository { get; init; }
        public string? releases_url { get; init; }

        public ReleaseMetadataSource ToModel()
        {
            if (string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(releases_url))
            {
                throw new InvalidOperationException("Release metadata source.repository and source.releases_url are required.");
            }

            return new ReleaseMetadataSource(repository, releases_url);
        }
    }

    private sealed class ChannelsDocument
    {
        public ReleaseChannelDocument? stable { get; init; }
        public ReleaseChannelDocument? prerelease { get; init; }

        public ReleaseMetadataChannels ToModel()
        {
            return new ReleaseMetadataChannels(
                Stable: stable?.ToModel() ?? throw new InvalidOperationException("Release metadata channels.stable is required."),
                Prerelease: prerelease?.ToModel());
        }
    }

    private sealed class ReleaseChannelDocument
    {
        public string? version { get; init; }
        public string? tag { get; init; }
        public DateTimeOffset published_at { get; init; }
        public string? release_url { get; init; }
        public string? notes_url { get; init; }
        public string? changelog_url { get; init; }
        public CompatibilityDocument? compatibility { get; init; }

        public ReleaseChannel ToModel()
        {
            if (string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(tag)
                || string.IsNullOrWhiteSpace(release_url)
                || string.IsNullOrWhiteSpace(notes_url)
                || string.IsNullOrWhiteSpace(changelog_url)
                || compatibility is null)
            {
                throw new InvalidOperationException("Release metadata channel payload is missing required fields.");
            }

            return new ReleaseChannel(
                Version: version,
                Tag: tag,
                PublishedAt: published_at,
                ReleaseUrl: release_url,
                NotesUrl: notes_url,
                ChangelogUrl: changelog_url,
                Compatibility: compatibility.ToModel());
        }
    }

    private sealed class CompatibilityDocument
    {
        public string? minimum_cli_version { get; init; }
        public string? notes { get; init; }

        public ReleaseCompatibility ToModel()
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                throw new InvalidOperationException("Release metadata compatibility.notes is required.");
            }

            return new ReleaseCompatibility(minimum_cli_version, notes);
        }
    }
}
