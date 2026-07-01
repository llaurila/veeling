namespace Veeling.CLI.Providers;

public enum LLMChatMessageRole
{
    System,
    User,
    Assistant
}

public record LLMChatMessage(LLMChatMessageRole Role, string Content);
