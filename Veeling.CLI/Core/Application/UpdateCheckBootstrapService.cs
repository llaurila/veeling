namespace Veeling.Core.Application;

public sealed class UpdateCheckBootstrapService(UpdateAdvisoryApplicationService advisoryService)
{
    private readonly UpdateAdvisoryApplicationService advisoryService = advisoryService;

    public void TriggerBackgroundCheck(Action<string>? writeLine, string currentVersion, bool enabled, string? installSourceHint)
    {
        if (!enabled)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                UpdateAdvisoryResult result = await advisoryService.BuildAdvisoryAsync(
                    currentVersion,
                    new UpdateAdvisoryRequest(
                        Enabled: enabled,
                        Manual: false,
                        IncludePrerelease: false,
                        InstallSourceHint: installSourceHint));

                if (!result.UpdateAvailable || writeLine is null)
                {
                    return;
                }

                writeLine($"Update available: {currentVersion} -> {result.LatestVersion}");
                if (!string.IsNullOrWhiteSpace(result.ReleaseUrl))
                {
                    writeLine($"Release notes: {result.ReleaseUrl}");
                }

                if (!string.IsNullOrWhiteSpace(result.Guidance))
                {
                    writeLine(result.Guidance);
                }
            }
            catch
            {
                // Never throw from bootstrap path; update checks are advisory and non-fatal.
            }
        });
    }
}
