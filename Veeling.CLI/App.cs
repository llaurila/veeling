using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using Veeling.CLI.Commands;
using Veeling.CLI.Exceptions;
using Veeling.Core.Application;

namespace Veeling.CLI;

public class App
{
    private readonly UpdateCheckBootstrapService updateCheckBootstrapService;

    public App(IServiceProvider serviceProvider)
    {
        RootCommand = new RootCommand("AI-powered translation management tool.");
        updateCheckBootstrapService = serviceProvider.GetRequiredService<UpdateCheckBootstrapService>();

        RootCommand.Options.Add(new System.CommandLine.Option<bool>("--check-updates")
        {
            Description = "Run an explicit update check for this invocation.",
            Required = false,
            Arity = ArgumentArity.Zero
        });

        foreach (ICliCommand cliCommand in serviceProvider.GetServices<ICliCommand>())
        {
            RootCommand.Subcommands.Add(cliCommand.Command);
        }
    }

    public RootCommand RootCommand { get; }

    public async Task<int> RunAsync(string[] args)
    {
        bool manual = args.Any(static arg => string.Equals(arg, "--check-updates", StringComparison.Ordinal));
        bool enabled = IsUpdateCheckEnabled();

        updateCheckBootstrapService.TriggerBackgroundCheck(
            writeLine: static line => Console.Error.WriteLine(line),
            currentVersion: VersionInfo.GetCurrentVersion(),
            enabled: enabled || manual,
            installSourceHint: DetectInstallSource());

        try
        {
            return await RootCommand.Parse(args).InvokeAsync();
        }
        catch (CommandExecutionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ex.ExitCode;
        }
        catch (VeelingException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool IsUpdateCheckEnabled()
    {
        try
        {
            VeelingConfig config = new();
            string? value = config.GetValue("update_check_enabled");
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return !string.Equals(value.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static string? DetectInstallSource()
    {
        string? appContextName = AppContext.GetData("APP_CONTEXT_DEPS_FILES")?.ToString();
        if (!string.IsNullOrWhiteSpace(appContextName) && appContextName.Contains(".store", StringComparison.OrdinalIgnoreCase))
        {
            return "nuget";
        }

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return "archive";
        }

        return null;
    }
}
