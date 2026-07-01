namespace Veeling.CLI.Exceptions;

public sealed class PersistenceException : CommandExecutionException
{
    public PersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
