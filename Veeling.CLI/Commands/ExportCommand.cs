using System.CommandLine;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Commands;

public enum ExportFormat
{
    Yaml,
    Json
}

internal static class ExportFormatExtensions
{
    public static ExportOutputFormat ToOutputFormat(this ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Yaml => ExportOutputFormat.Yaml,
            ExportFormat.Json => ExportOutputFormat.Json,
            _ => throw new InvalidOperationException("Unsupported export format.")
        };
    }
}

public class ExportCommand : ICliCommand
{
    private readonly ExportApplicationService exportApplicationService;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();
    private readonly Argument<string?> selectorArgument = CommandUtils.CreateOptionalSelectorArgument();

    private readonly Option<string> formatOption = new("--format")
    {
        Description = "Format of the export. Possible values: yaml (default), json",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-f" }
    };

    public ExportCommand(ExportApplicationService exportApplicationService)
    {
        this.exportApplicationService = exportApplicationService;

        Command = new Command("export", "Export/view records (selector optional; defaults to *.*:*).");
        Command.Options.Add(projectFileOption);
        Command.Options.Add(formatOption);
        Command.Arguments.Add(selectorArgument);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        Project? project = CommandUtils.GetProject(parseResult, projectFileOption);
        if (project is null)
        {
            Console.Error.WriteLine("No project found.");
            return 1;
        }

        ExportFormat? format = GetFormat(parseResult);
        if (format is null)
        {
            string supportedFormats = string.Join(", ", Enum.GetNames<ExportFormat>());
            Console.Error.WriteLine($"Invalid format specified. Supported formats: {supportedFormats}");
            return 1;
        }

        try
        {
            ExportCommandRequest request = new(
                Project: project,
                Selector: parseResult.GetValue(selectorArgument),
                Format: format.Value.ToOutputFormat());

            string output = exportApplicationService.Execute(request);
            Console.WriteLine(output);
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        return 0;
    }

    private ExportFormat? GetFormat(ParseResult parseResult)
    {
        string formatString = parseResult.GetValue(formatOption) ?? "yaml";
        bool success = Enum.TryParse(formatString, ignoreCase: true, out ExportFormat format);
        return success ? format : null;
    }
}
