namespace Veeling.CLI.Exceptions;

public sealed class ProviderAuthenticationException : CommandExecutionException
{
    public ProviderAuthenticationException(string message, Exception innerException)
        : base(message, innerException, exitCode: 4)
    {
    }
}
