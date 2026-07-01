namespace Veeling.CLI.Exceptions;

public sealed class MissingSourceDataException : CommandExecutionException
{
    public MissingSourceDataException(string message)
        : base(message, exitCode: 5)
    {
    }
}
