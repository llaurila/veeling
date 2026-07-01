namespace Veeling.CLI.Exceptions;

public sealed class ProjectPublishException : CommandExecutionException
{
    public ProjectPublishException(string message)
        : base(message)
    {
    }

    public ProjectPublishException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
