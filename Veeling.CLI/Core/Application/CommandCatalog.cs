using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Veeling.CLI.Commands;

namespace Veeling.Core.Application;

public interface ICommandCatalogBuilder
{
    CommandCatalog Build();
}

public sealed record CommandCatalog(
    IReadOnlyList<CommandCatalogEntry> Commands,
    IReadOnlyDictionary<string, string> CuratedHints)
{
    public CommandCatalogEntry? FindByPath(IReadOnlyList<string> pathSegments)
    {
        if (pathSegments.Count == 0)
        {
            return null;
        }

        return Commands.FirstOrDefault(command =>
            command.PathSegments.Count == pathSegments.Count
            && command.PathSegments.Zip(pathSegments).All(pair =>
                string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase)
            ));
    }
}

public sealed record CommandCatalogEntry(
    string Name,
    string Description,
    IReadOnlyList<string> PathSegments,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<CommandCatalogOption> Options,
    IReadOnlyList<CommandCatalogArgument> Arguments,
    string? CuratedHint);

public sealed record CommandCatalogOption(
    string Name,
    string Description,
    IReadOnlyList<string> Aliases,
    bool RequiresValue);

public sealed record CommandCatalogArgument(
    string Name,
    string Description,
    int MinimumArity,
    int MaximumArity);

public sealed class LiveCommandCatalogBuilder(IServiceProvider serviceProvider) : ICommandCatalogBuilder
{
    private static readonly IReadOnlyDictionary<string, string> HintByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["translate"] = "If --from is omitted, the project master language is used as default source.",
        ["modify"] = "Wildcard record specs can be high-impact and may require --force.",
        ["config"] = "--global can operate outside a project; local/default scope expects project context.",
        ["onboard"] = "Interactive setup command. In v1 natural-language flow, this command is suggestion-only.",
        ["update"] = "Meta/update utility command. In v1 natural-language flow, this command is suggestion-only.",
        ["ai"] = "Never resolve to ai/ask recursively. ai is for intent-to-command mapping only."
    };

    public CommandCatalog Build()
    {
        List<CommandCatalogEntry> entries = [];

        IEnumerable<ICliCommand> cliCommands = serviceProvider.GetServices<ICliCommand>();

        foreach (ICliCommand cliCommand in cliCommands)
        {
            Visit(entries, cliCommand.Command, [cliCommand.Command.Name]);
        }

        return new CommandCatalog(entries, HintByPath);
    }

    private static void Visit(List<CommandCatalogEntry> entries, Command command, IReadOnlyList<string> pathSegments)
    {
        string pathKey = string.Join(' ', pathSegments);

        entries.Add(new CommandCatalogEntry(
            Name: command.Name,
            Description: command.Description ?? string.Empty,
            PathSegments: [.. pathSegments],
            Aliases: [.. command.Aliases],
            Options: [.. command.Options.Select(option => new CommandCatalogOption(
                Name: option.Name,
                Description: option.Description ?? string.Empty,
                Aliases: [.. option.Aliases],
                RequiresValue: option.Arity.MaximumNumberOfValues > 0
            ))],
            Arguments: [.. command.Arguments.Select(argument => new CommandCatalogArgument(
                Name: argument.Name,
                Description: argument.Description ?? string.Empty,
                MinimumArity: argument.Arity.MinimumNumberOfValues,
                MaximumArity: argument.Arity.MaximumNumberOfValues
            ))],
            CuratedHint: HintByPath.TryGetValue(pathKey, out string? exactHint)
                ? exactHint
                : HintByPath.TryGetValue(pathSegments[0], out string? rootHint) ? rootHint : null
        ));

        foreach (Command subcommand in command.Subcommands)
        {
            Visit(entries, subcommand, [.. pathSegments, subcommand.Name]);
        }
    }
}
