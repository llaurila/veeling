namespace Veeling.CLI.Exceptions;

public sealed class GlossaryValidationException : CommandExecutionException
{
    public GlossaryValidationException(string message)
        : base(message)
    {
    }

    public GlossaryValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
