using System.CommandLine;
using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

public sealed class UpdateCommand : ICliCommand
{
    private readonly UpdateAdvisoryApplicationService advisoryService;

    private readonly Option<bool> prereleaseOption = new("--prerelease")
    {
        Description = "Include prerelease channel when checking for updates.",
        Required = false,
        Arity = ArgumentArity.Zero
    };

    private readonly Option<string> sourceOption = new("--source")
    {
        Description = "Optional install source hint (nuget|archive).",
        Required = false,
        Arity = ArgumentArity.ExactlyOne
    };

    public UpdateCommand(UpdateAdvisoryApplicationService advisoryService)
    {
        this.advisoryService = advisoryService;

        Command update = new("update", "Update utilities.");
        Command check = new("check", "Manually check for a newer Veeling release.");
        Command self = new("self", "Print safe, channel-specific self-update guidance.");

        check.Options.Add(prereleaseOption);
        check.Options.Add(sourceOption);
        check.SetAction(ExecuteCheckAsync);

        self.Options.Add(sourceOption);
        self.SetAction(ExecuteSelf);

        update.Subcommands.Add(check);
        update.Subcommands.Add(self);
        Command = update;
    }

    public Command Command { get; }

    private async Task<int> ExecuteCheckAsync(ParseResult parseResult)
    {
        bool includePrerelease = parseResult.GetValue(prereleaseOption);
        string? source = parseResult.GetValue(sourceOption);

        UpdateAdvisoryResult result = await advisoryService.BuildAdvisoryAsync(
            currentVersion: VersionInfo.GetCurrentVersion(),
            request: new UpdateAdvisoryRequest(
                Enabled: true,
                Manual: true,
                IncludePrerelease: includePrerelease,
                InstallSourceHint: source));

        if (result.CheckFailed)
        {
            Console.WriteLine("Update check could not be completed (offline/timeout). Command flow remains unaffected.");
            return 0;
        }

        if (!result.UpdateAvailable)
        {
            Console.WriteLine("You are running the latest available version.");
            return 0;
        }

        Console.WriteLine($"Update available: {VersionInfo.GetCurrentVersion()} -> {result.LatestVersion}");
        if (!string.IsNullOrWhiteSpace(result.ReleaseUrl))
        {
            Console.WriteLine($"Release notes: {result.ReleaseUrl}");
        }

        if (!string.IsNullOrWhiteSpace(result.Guidance))
        {
            Console.WriteLine(result.Guidance);
        }

        return 0;
    }

    private int ExecuteSelf(ParseResult parseResult)
    {
        string? source = parseResult.GetValue(sourceOption);
        string guidance = advisoryService.BuildSelfUpdateGuidance(source);

        Console.WriteLine("Self-update is user-controlled. Veeling will not mutate your system automatically.");
        Console.WriteLine(guidance);
        return 0;
    }
}
