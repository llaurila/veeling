namespace Veeling.CLI.Exceptions;

public sealed class TranslationResponseException : CommandExecutionException
{
    public TranslationResponseException(string message, Exception innerException)
        : base(message, innerException, exitCode: 2)
    {
    }
}
