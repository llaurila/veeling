using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

public sealed class AiCommand : ICliCommand
{
    private readonly IntentResolutionApplicationService intentResolutionApplicationService;
    private readonly IServiceProvider serviceProvider;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();

    private readonly Argument<string> intentArgument = new("intent")
    {
        Description = "Natural-language intent to resolve into a Veeling command.",
        Arity = ArgumentArity.ExactlyOne
    };

    public AiCommand(IntentResolutionApplicationService intentResolutionApplicationService, IServiceProvider serviceProvider)
    {
        this.intentResolutionApplicationService = intentResolutionApplicationService;
        this.serviceProvider = serviceProvider;

        Command = new Command("ai", "Resolve natural-language intent into a validated Veeling command.")
        {
            Aliases = { "ask" }
        };

        Command.Options.Add(projectFileOption);
        Command.Arguments.Add(intentArgument);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        string intent = parseResult.GetValue(intentArgument)!;
        FileInfo projectFile = CommandUtils.GetProjectFileInfo(parseResult, projectFileOption);

        IntentResolutionResult result = intentResolutionApplicationService.Resolve(new IntentResolutionRequest(intent, projectFile));

        if (result.Outcome != IntentResolutionOutcome.Resolved || result.Command is null)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        ResolvedCommand command = result.Command;

        Console.WriteLine("Resolved command preview:");
        Console.WriteLine(command.CanonicalInvocation);
        if (!string.IsNullOrWhiteSpace(command.Explanation))
        {
            Console.WriteLine(command.Explanation);
        }

        if (command.IsSuggestionOnly)
        {
            Console.WriteLine(command.SuggestionReason ?? "Suggestion-only in v1.");
            return 0;
        }

        if (!ConfirmExecution())
        {
            Console.Error.WriteLine("Aborted by user.");
            return 1;
        }

        App app = serviceProvider.GetRequiredService<App>();
        string[] dispatchArgs = BuildDispatchArgs(command, parseResult.GetValue(projectFileOption));
        return app.RootCommand.Parse(dispatchArgs).Invoke();
    }

    private static string[] BuildDispatchArgs(ResolvedCommand command, string? projectFilePath)
    {
        List<string> tokens = [.. command.PathSegments];

        if (command.SupportsProjectFileOption
            && !string.IsNullOrWhiteSpace(projectFilePath)
            && !command.Options.Keys.Any(option => string.Equals(option, "--project-file", StringComparison.OrdinalIgnoreCase)))
        {
            tokens.Add("--project-file");
            tokens.Add(projectFilePath!);
        }

        foreach ((string option, string? value) in command.Options)
        {
            tokens.Add(option);
            if (value is not null)
            {
                tokens.Add(value);
            }
        }

        tokens.AddRange(command.Arguments);
        return [.. tokens];
    }

    private static bool ConfirmExecution()
    {
        Console.Write("Execute this command? [y/N] ");
        string? input = Console.ReadLine();
        return string.Equals(input?.Trim(), "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }
}
