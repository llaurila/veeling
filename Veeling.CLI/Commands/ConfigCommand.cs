using System.CommandLine;
using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

public class ConfigCommand : ICliCommand
{
    private readonly ConfigApplicationService configApplicationService;
    private readonly Option<string> projectFileOption = CommandUtils.CreateProjectFileOption();

    private readonly Option<string> keyOption = new("--key")
    {
        Description = "Name of the configuration value to read or write.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-k" }
    };

    private readonly Option<string> valueOption = new("--value")
    {
        Description = "Set a new value to the configuration option.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-v" }
    };

    private readonly Option<bool> localOption = new("--local")
    {
        Description = "Operate on local config file in the selected project directory.",
        Required = false,
        Arity = ArgumentArity.Zero,
        Aliases = { "-l" }
    };

    private readonly Option<bool> globalOption = new("--global")
    {
        Description = "Set the configuration value globally (projects can override).",
        Required = false,
        Arity = ArgumentArity.Zero,
        Aliases = { "-g" }
    };

    public ConfigCommand(ConfigApplicationService configApplicationService)
    {
        this.configApplicationService = configApplicationService;

        Command = new Command("config", "Get or set configuration variables.");
        Command.Options.Add(projectFileOption);
        Command.Options.Add(keyOption);
        Command.Options.Add(valueOption);
        Command.Options.Add(localOption);
        Command.Options.Add(globalOption);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        FileInfo projectFile = CommandUtils.GetProjectFileInfo(parseResult, projectFileOption);
        DirectoryInfo configDirectory = CommandUtils.GetProjectDirectory(parseResult, projectFileOption);

        try
        {
            ConfigCommandRequest request = new(
                ProjectFile: projectFile,
                ConfigDirectory: configDirectory,
                Local: parseResult.GetValue(localOption),
                Global: parseResult.GetValue(globalOption),
                Key: parseResult.GetValue(keyOption),
                Value: parseResult.GetValue(valueOption)
            );

            ConfigCommandResult result = configApplicationService.Execute(request);

            foreach (string line in result.OutputLines)
            {
                Console.WriteLine(line);
            }
        }
        catch (Exceptions.CommandExecutionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ex.ExitCode;
        }

        return 0;
    }
}
