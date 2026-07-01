using System.CommandLine;
using System.Text.Json;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Commands;

public class ModifyCommand : ICliCommand
{
    private readonly ModifyApplicationService modifyApplicationService;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();
    private readonly Argument<string> recordSpecArgument = CommandUtils.CreateRecordSpecArgument();

    private readonly Option<bool> stdinOption = new("--stdin")
    {
        Description = "Read the field value from standard input as JSON.",
        Required = false,
        Arity = ArgumentArity.Zero
    };

    private readonly Option<string> valueOption = new("--value")
    {
        Description = "New field value to set.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-v" }
    };

    private readonly Option<string> byOption = new("--by")
    {
        Description = "Name of the person or instance making the change.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-b" }
    };

    private readonly Option<string> commentOption = new("--comment")
    {
        Description = "Additional information about the translation.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-c" }
    };

    private readonly Option<string> statusOption = new("--status")
    {
        Description = "Set the status (New, NeedsReview [default], Bad, Approved) of the record.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-s" }
    };

    private readonly Option<bool> forceOption = new("--force")
    {
        Description = "Allow setting multiple records to the same value when using wildcards.",
        Required = false,
        Arity = ArgumentArity.Zero,
        Aliases = { "-f" }
    };

    public ModifyCommand(ModifyApplicationService modifyApplicationService)
    {
        this.modifyApplicationService = modifyApplicationService;

        Command = new Command("modify", "Modify a record.");
        Command.Options.Add(projectFileOption);
        Command.Options.Add(stdinOption);
        Command.Options.Add(valueOption);
        Command.Options.Add(byOption);
        Command.Options.Add(commentOption);
        Command.Options.Add(statusOption);
        Command.Options.Add(forceOption);
        Command.Arguments.Add(recordSpecArgument);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        if (!CommandUtils.TryGetRecordSpec(parseResult, recordSpecArgument, out RecordFilter recordSpec)) return 1;

        Project? project = CommandUtils.GetProject(parseResult, projectFileOption);
        if (project is null)
        {
            Console.Error.WriteLine("No project found.");
            return 1;
        }

        VeelingConfig config = new(project.Directory);
        string? by = parseResult.GetValue(byOption) ?? config.GetValue("username");

        if (by is null)
        {
            Console.Error.WriteLine(
                "No username specified for the change. Use the --by option or set a default username in the config file."
            );
            return 1;
        }

        if (!TryGetNewValue(parseResult, out string? value))
        {
            return 1;
        }

        if (!TryGetStatus(parseResult, out DataStatus? status))
        {
            return 1;
        }

        try
        {
            ModifyCommandRequest request = new(
                Project: project,
                RecordSpec: recordSpec,
                By: by,
                Value: value,
                Status: status,
                Comment: parseResult.GetValue(commentOption),
                Force: parseResult.GetValue(forceOption)
            );

            ModifyCommandResult result = modifyApplicationService.Execute(request);

            if (!result.HasChanges)
            {
                Console.WriteLine("No change.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        return 0;
    }

    private bool TryGetNewValue(ParseResult parseResult, out string? value)
    {
        value = parseResult.GetValue(valueOption);
        bool useStdin = parseResult.GetValue(stdinOption);

        if (!string.IsNullOrWhiteSpace(value) && useStdin)
        {
            Console.Error.WriteLine("Specify either --value or --stdin, not both.");
            value = null;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!useStdin)
        {
            value = null;
            return true;
        }

        return TryReadJsonStringFromStdin(out value);
    }

    private bool TryGetStatus(ParseResult parseResult, out DataStatus? status)
    {
        if (parseResult.GetValue(statusOption) is string statusStr)
        {
            if (Enum.TryParse(statusStr, ignoreCase: true, out DataStatus parsedStatus) && parsedStatus != DataStatus.Unknown)
            {
                status = parsedStatus;
                return true;
            }

            Console.Error.WriteLine($"Invalid status value: {statusStr}. Valid values are: New, NeedsReview, Approved, Bad.");
            status = null;
            return false;
        }

        status = null;
        return true;
    }

    private static bool TryReadJsonStringFromStdin(out string? value)
    {
        string input = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(input))
        {
            value = null;
            return true;
        }

        try
        {
            value = JsonSerializer.Deserialize<string>(input);
            if (value is null)
            {
                Console.Error.WriteLine("Input JSON must be a string value.");
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("Invalid JSON input.");
            value = null;
            return false;
        }
    }
}
