using Veeling.Core.Application;

namespace Veeling.CLI.Providers;

public interface IIntentParserProvider
{
    IntentParserResponse Parse(IntentParserProviderSelection selection, IntentParserRequest request);
}
