using System.CommandLine;
using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

public class StatusCommand : ICliCommand
{
    private readonly StatusApplicationService statusApplicationService;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();

    public StatusCommand(StatusApplicationService statusApplicationService)
    {
        this.statusApplicationService = statusApplicationService;

        Command = new Command("status", "Show the project status.");
        Command.Options.Add(projectFileOption);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        Project? project = CommandUtils.GetProject(parseResult, projectFileOption);
        if (project is null) return 1;

        try
        {
            StatusCommandResult result = statusApplicationService.Execute(project);

            foreach (StatusIssueOutput output in result.Output)
            {
                if (output.Kind == StatusOutputKind.NoIssues)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"No issues found in '{result.ProjectName}'.");
                    Console.ResetColor();
                    continue;
                }

                const int maxLabelLength = 15;

                Console.ForegroundColor = GetStatusColor(output.Status!.Value);
                string statusMessage = GetStatusMessage(output.Status.Value);

                Console.WriteLine(
                    $"{statusMessage + ':',-maxLabelLength} {output.Result!.RecordLocator}"
                );

                Console.ResetColor();
            }

            return result.ExitCode;
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string GetStatusMessage(DataRetrieveResultStatus status)
    {
        return status switch
        {
            DataRetrieveResultStatus.Missing => "Missing",
            DataRetrieveResultStatus.MissingMaster => "Missing master",
            DataRetrieveResultStatus.SourceChange => "Source change",
            DataRetrieveResultStatus.NeedsApproval => "Needs approval",
            _ => "Unknown issue"
        };
    }

    private static ConsoleColor GetStatusColor(DataRetrieveResultStatus status)
    {
        return status switch
        {
            DataRetrieveResultStatus.Missing => ConsoleColor.Red,
            DataRetrieveResultStatus.MissingMaster => ConsoleColor.Red,
            DataRetrieveResultStatus.SourceChange => ConsoleColor.DarkYellow,
            DataRetrieveResultStatus.NeedsApproval => ConsoleColor.DarkYellow,
            _ => ConsoleColor.White
        };
    }
}
