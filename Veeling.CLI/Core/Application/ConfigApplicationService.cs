using Veeling.CLI.Exceptions;
using Veeling.CLI;

namespace Veeling.Core.Application;

public sealed record ConfigCommandRequest(
    FileInfo ProjectFile,
    DirectoryInfo ConfigDirectory,
    bool Local,
    bool Global,
    string? Key,
    string? Value
);

public sealed record ConfigCommandResult(IReadOnlyList<string> OutputLines);

public sealed class ConfigApplicationService
{
    private readonly IGlobalConfigFileLocator globalConfigFileLocator;

    public ConfigApplicationService(IGlobalConfigFileLocator globalConfigFileLocator)
    {
        this.globalConfigFileLocator = globalConfigFileLocator;
    }

    public ConfigCommandResult Execute(ConfigCommandRequest request)
    {
        if (request.Local && request.Global)
        {
            throw new CommandExecutionException("Cannot use --local and --global together. Choose exactly one scope.");
        }

        if (!request.Global && !request.ProjectFile.Exists)
        {
            throw new CommandExecutionException(string.Join(
                Environment.NewLine,
                "No project found in the selected directory.",
                $"If you want to read or write global config outside a project, use --global."
            ));
        }

        if (request.Value is not null && string.IsNullOrWhiteSpace(request.Key))
        {
            throw new CommandExecutionException("--value requires --key.");
        }

        VeelingConfig config = request.Global
            ? new VeelingConfig(globalConfigFileLocator: globalConfigFileLocator)
            : new VeelingConfig(request.ConfigDirectory, globalConfigFileLocator);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                IEnumerable<KeyValuePair<string, string>> values = request switch
                {
                    { Global: true } => config.GetAllGlobalValues(),
                    { Local: true } => config.GetAllLocalValues(),
                    _ => config.GetAllValues()
                };

                return new ConfigCommandResult([
                    .. values.Select(kvp => $"{kvp.Key} = {kvp.Value}")
                ]);
            }

            if (request.Value is null)
            {
                string? currentValue = request switch
                {
                    { Global: true } => config.GetGlobalValue(request.Key),
                    { Local: true } => config.GetLocalValue(request.Key),
                    _ => config.GetValue(request.Key)
                };

                return currentValue is null
                    ? new ConfigCommandResult([])
                    : new ConfigCommandResult([currentValue]);
            }

            if (request.Global)
            {
                config.SetGlobalValue(request.Key, request.Value);
            }
            else
            {
                config.SetLocalValue(request.Key, request.Value);
            }
        }
        catch (ArgumentException ex)
        {
            throw new CommandExecutionException(ex.Message, ex);
        }

        return new ConfigCommandResult([]);
    }
}
