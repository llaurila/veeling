using System.CommandLine;
using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

public class PublishCommand : ICliCommand
{
    private readonly PublishApplicationService publishApplicationService;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();

    public const int Exit_Success = 0;
    public const int Exit_NotFound = 1;

    public PublishCommand(PublishApplicationService publishApplicationService)
    {
        this.publishApplicationService = publishApplicationService;

        Command = new Command("publish", "Publish content.");
        Command.Options.Add(projectFileOption);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        Project? project = CommandUtils.GetProject(parseResult, projectFileOption);
        if (project is null) return Exit_NotFound;

        Console.WriteLine(publishApplicationService.Execute(project));
        return Exit_Success;
    }
}
