namespace Veeling.CLI.Exceptions;

public sealed class ProviderExecutionException : CommandExecutionException
{
    public ProviderExecutionException(string message, Exception innerException)
        : base(message, innerException, exitCode: 3)
    {
    }
}
