using Veeling.Models;

namespace Veeling.CLI;

public class Translation
{
    public required RecordLocator Field { get; init; }

    public required string OriginalValue { get; init; }

    public required string TranslatedValue { get; init; }
}
