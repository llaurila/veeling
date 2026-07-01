using Veeling.Models;

namespace Veeling.CLI.Exceptions;

public class RecordSpecVeelingException(RecordFilter recordSpec, string message) : VeelingException(message)
{
    public RecordFilter RecordSpec { get; private set; } = recordSpec;
}
