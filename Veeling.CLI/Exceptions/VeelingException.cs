namespace Veeling.CLI.Exceptions;

public class VeelingException : Exception
{
    public VeelingException(string message)
        : base(message)
    {
    }

    public VeelingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
