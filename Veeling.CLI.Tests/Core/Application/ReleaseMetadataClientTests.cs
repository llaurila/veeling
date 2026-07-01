using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veeling.CLI.Configuration;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class ReleaseMetadataClientTests
{
    [Fact]
    public async Task FetchAsync_ValidPayload_ParsesMetadata()
    {
        string payload = """
        {
          "schema_version": "1.0",
          "generated_at": "2026-06-26T00:00:00Z",
          "source": {
            "repository": "https://github.com/llaurila/veeling",
            "releases_url": "https://github.com/llaurila/veeling/releases"
          },
          "channels": {
            "stable": {
              "version": "0.4.1",
              "tag": "v0.4.1",
              "published_at": "2026-06-26T00:00:00Z",
              "release_url": "https://github.com/llaurila/veeling/releases/tag/v0.4.1",
              "notes_url": "https://github.com/llaurila/veeling/releases/tag/v0.4.1",
              "changelog_url": "https://github.com/llaurila/veeling/blob/main/CHANGELOG.md",
              "compatibility": {
                "minimum_cli_version": null,
                "notes": "No compatibility floor declared yet."
              }
            },
            "prerelease": null
          }
        }
        """;

        HttpClient httpClient = new(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

        ReleaseMetadataClient client = CreateClient(httpClient);

        ReleaseMetadata metadata = await client.FetchAsync(CancellationToken.None);

        Assert.Equal("1.0", metadata.SchemaVersion);
        Assert.Equal("0.4.1", metadata.Channels.Stable.Version);
        Assert.Null(metadata.Channels.Prerelease);
    }

    [Fact]
    public async Task FetchAsync_MissingStable_Throws()
    {
        string payload = """
        {
          "schema_version": "1.0",
          "generated_at": "2026-06-26T00:00:00Z",
          "source": {
            "repository": "https://github.com/llaurila/veeling",
            "releases_url": "https://github.com/llaurila/veeling/releases"
          },
          "channels": {
            "stable": null,
            "prerelease": null
          }
        }
        """;

        HttpClient httpClient = new(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

        ReleaseMetadataClient client = CreateClient(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchAsync(CancellationToken.None));
    }

    private static ReleaseMetadataClient CreateClient(HttpClient httpClient)
    {
        return new ReleaseMetadataClient(
            httpClient,
            Options.Create(new UpdateCheckOptions
            {
                MetadataUrl = "https://example.com/release-metadata/latest.json"
            }),
            NullLogger<ReleaseMetadataClient>.Instance);
    }

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }
}
