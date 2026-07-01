using Veeling.Models;

namespace Veeling.CLI.Exceptions;

public class AbsoluteRecordSpecRequiredVeelingException : RecordSpecVeelingException
{
    public AbsoluteRecordSpecRequiredVeelingException(RecordFilter recordSpec)
        : base(recordSpec, $"Absolute record specification required: '{recordSpec}'")
    {
    }
}
