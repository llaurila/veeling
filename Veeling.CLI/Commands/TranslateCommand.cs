using System.CommandLine;
using Veeling.CLI.Exceptions;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Commands;

public class TranslateCommand : ICliCommand
{
    private readonly TranslateApplicationService translateApplicationService;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();

    private readonly Option<string> fromOption = new("--from")
    {
        Description = "Source language.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-f" }
    };

    private readonly Option<string> toOption = new("--to")
    {
        Description = "Target language(s), comma-separated, or * for all languages.",
        Required = true,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-t" }
    };

    private readonly Option<bool> dryRunOption = new("--dry-run")
    {
        Description = "Show which records would be affected.",
        Required = false,
        Arity = ArgumentArity.Zero,
        Aliases = { "-d" }
    };

    private readonly Option<bool> changedOption = new("--changed")
    {
        Description = "Include already translated records whose master/source content has changed.",
        Required = false,
        Arity = ArgumentArity.Zero
    };

    public TranslateCommand(TranslateApplicationService translateApplicationService)
    {
        this.translateApplicationService = translateApplicationService;

        Command = new Command("translate", "Translate content from one language into others.");
        Command.Options.Add(projectFileOption);
        Command.Options.Add(fromOption);
        Command.Options.Add(toOption);
        Command.Options.Add(dryRunOption);
        Command.Options.Add(changedOption);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        Project? project = CommandUtils.GetProject(parseResult, projectFileOption);
        if (project is null) return 1;

        Language? from = GetSourceLanguage(parseResult, project);
        if (from is null) return 1;

        Language[] toLanguages = ParseTargetLanguages(parseResult, project, from);
        if (toLanguages.Length == 0)
        {
            Console.Error.WriteLine("No target languages specified, quitting.");
            return 1;
        }

        bool dryRun = parseResult.GetValue(dryRunOption);
        bool changed = parseResult.GetValue(changedOption);

        try
        {
            TranslateCommandResult result = translateApplicationService.Execute(project, from, toLanguages, dryRun, changed);

            if (result.Warning is not null)
            {
                Console.Error.WriteLine(result.Warning);
            }

            foreach (string line in result.OutputLines)
            {
                Console.WriteLine(line);
            }
        }
        catch (CommandExecutionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ex.ExitCode;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        return 0;
    }

    private Language[] ParseTargetLanguages(ParseResult parseResult, Project project, Language from)
    {
        string toValue = parseResult.GetValue(toOption)!;
        Language[] result = CommandUtils.ParseTargetLanguages(toValue, project);
        return [.. result.Where(lang => !lang.Equals(from))];
    }

    private Language? GetSourceLanguage(ParseResult parseResult, Project project)
    {
        string? fromValue = parseResult.GetValue(fromOption);

        if (fromValue is null)
        {
            return project.Model.MasterLanguage;
        }

        try
        {
            Language fromLanguage = new(fromValue);
            if (!project.SupportsLanguage(fromLanguage))
            {
                Console.Error.WriteLine(
                    $"The source language '{fromLanguage.Code}' is not supported by the project."
                );
                return null;
            }

            return fromLanguage;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine(
                $"The source language code '{fromValue}' is not valid."
            );
            return null;
        }
    }
}
