namespace Veeling.CLI.Exceptions;

public class CommandExecutionException : VeelingException
{
    public CommandExecutionException(string message, int exitCode = 1)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public CommandExecutionException(string message, Exception innerException, int exitCode = 1)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
