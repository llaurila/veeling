using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class UpdateAdvisoryApplicationServiceTests
{
    [Fact]
    public async Task BuildAdvisoryAsync_Disabled_ReturnsNoCheck()
    {
        UpdateAdvisoryApplicationService service = new(new FakeUpdateCheckService(new UpdateCheckResult(false, true, TestData.Metadata("0.9.0"), null, DateTimeOffset.UtcNow)));

        UpdateAdvisoryResult result = await service.BuildAdvisoryAsync("0.4.1", new UpdateAdvisoryRequest(false, false, false, null));

        Assert.False(result.UpdateAvailable);
        Assert.False(result.CheckFailed);
    }

    [Fact]
    public async Task BuildAdvisoryAsync_UpdateAvailable_ReturnsGuidance()
    {
        UpdateAdvisoryApplicationService service = new(new FakeUpdateCheckService(new UpdateCheckResult(false, true, TestData.Metadata("0.9.0"), null, DateTimeOffset.UtcNow)));

        UpdateAdvisoryResult result = await service.BuildAdvisoryAsync("0.4.1", new UpdateAdvisoryRequest(true, true, false, "nuget"));

        Assert.True(result.UpdateAvailable);
        Assert.Contains("dotnet tool update --global veeling", result.Guidance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAdvisoryAsync_UnknownInstallSource_UsesSafeFallback()
    {
        UpdateAdvisoryApplicationService service = new(new FakeUpdateCheckService(new UpdateCheckResult(false, true, TestData.Metadata("0.9.0"), null, DateTimeOffset.UtcNow)));

        UpdateAdvisoryResult result = await service.BuildAdvisoryAsync("0.4.1", new UpdateAdvisoryRequest(true, true, false, null));

        Assert.True(result.UpdateAvailable);
        Assert.Contains("Install source not detected", result.Guidance, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeUpdateCheckService(UpdateCheckResult result) : IUpdateCheckService
    {
        private readonly UpdateCheckResult result = result;

        public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private static class TestData
    {
        public static ReleaseMetadata Metadata(string stableVersion)
        {
            return new ReleaseMetadata(
                "1.0",
                DateTimeOffset.UtcNow,
                new ReleaseMetadataSource("https://example/repo", "https://example/releases"),
                new ReleaseMetadataChannels(
                    new ReleaseChannel(
                        stableVersion,
                        $"v{stableVersion}",
                        DateTimeOffset.UtcNow,
                        "https://example/release",
                        "https://example/release",
                        "https://example/changelog",
                        new ReleaseCompatibility(null, "none")),
                    null));
        }
    }
}
