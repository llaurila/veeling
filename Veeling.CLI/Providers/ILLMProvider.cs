namespace Veeling.CLI.Providers;

public interface ILLMProvider
{
    LLMChatMessage Complete(params LLMChatMessage[] history);
}
