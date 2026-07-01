using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Commands;

public sealed class UpdateCommandTests
{
    [Fact]
    public async Task UpdateCheck_WhenUpdateAvailable_PrintsAdvisoryGuidance()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeReleaseMetadataClient("0.9.9"));
        App app = serviceProvider.GetRequiredService<App>();

        using ConsoleCapture console = new();
        int code = await app.RunAsync(["update", "check", "--source", "nuget"]);

        Assert.Equal(0, code);
        Assert.Contains("Update available", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet tool update --global veeling", console.StdOut.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateCheck_WhenProviderFails_IsNonFatal()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new ThrowingReleaseMetadataClient());
        App app = serviceProvider.GetRequiredService<App>();

        using ConsoleCapture console = new();
        int code = await app.RunAsync(["update", "check"]);

        Assert.Equal(0, code);
        Assert.Contains("could not be completed", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSelf_PrintsUserControlledGuidance_WithoutMutation()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider(new FakeReleaseMetadataClient("0.4.1"));
        App app = serviceProvider.GetRequiredService<App>();

        using ConsoleCapture console = new();
        int code = await app.RunAsync(["update", "self", "--source", "archive"]);

        Assert.Equal(0, code);
        Assert.Contains("user-controlled", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify SHA256SUMS", console.StdOut.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateServiceProvider(IReleaseMetadataClient metadataClient)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddVeelingCli(configuration);
        services.AddSingleton<IReleaseMetadataClient>(metadataClient);
        services.AddSingleton<IUpdateCheckService, UpdateCheckApplicationService>();
        services.AddSingleton<IUpdateCheckCache, NoopUpdateCheckCache>();
        return services.BuildServiceProvider();
    }

    private sealed class NoopUpdateCheckCache : IUpdateCheckCache
    {
        public UpdateCheckCacheEntry? Read() => null;

        public void Write(UpdateCheckCacheEntry entry)
        {
        }
    }

    private sealed class FakeReleaseMetadataClient(string version) : IReleaseMetadataClient
    {
        public Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReleaseMetadata(
                SchemaVersion: "1.0",
                GeneratedAt: DateTimeOffset.UtcNow,
                Source: new ReleaseMetadataSource("https://example/repo", "https://example/releases"),
                Channels: new ReleaseMetadataChannels(
                    Stable: new ReleaseChannel(
                        Version: version,
                        Tag: $"v{version}",
                        PublishedAt: DateTimeOffset.UtcNow,
                        ReleaseUrl: "https://example/releases/tag",
                        NotesUrl: "https://example/releases/tag",
                        ChangelogUrl: "https://example/changelog",
                        Compatibility: new ReleaseCompatibility(null, "none")),
                    Prerelease: null)));
        }
    }

    private sealed class ThrowingReleaseMetadataClient : IReleaseMetadataClient
    {
        public Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken)
        {
            throw new HttpRequestException("offline");
        }
    }
}
