using OpenAI.Chat;
using System.ClientModel;
using Veeling.CLI;

namespace Veeling.CLI.Providers;

public sealed class OpenAIProvider : ILLMProvider
{
    private readonly ChatClient chatClient;

    public OpenAIProvider(VeelingConfig config, string? modelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        string model = modelOverride
            ?? config.GetValue("openai_model")
            ?? throw new InvalidOperationException("Config key 'openai_model' is not set.");

        string apiKey = config.GetValue("openai_apikey")
            ?? throw new InvalidOperationException("Config key 'openai_apikey' is not set.");

        chatClient = new ChatClient(model: model, apiKey: apiKey);
    }

    public LLMChatMessage Complete(params LLMChatMessage[] history)
    {
        ChatMessage[] messages = [.. GetOpenAIChatMessages(history)];

        ClientResult<ChatCompletion> result = chatClient.CompleteChat(messages);

        return new LLMChatMessage
        (
            Role: LLMChatMessageRole.Assistant,
            Content: result.Value.Content[0].Text
        );
    }

    private static IEnumerable<ChatMessage> GetOpenAIChatMessages(LLMChatMessage[] history)
    {
        return history.Select(GetOpenAIChatMessage);
    }

    private static ChatMessage GetOpenAIChatMessage(LLMChatMessage message)
    {
        return message.Role switch
        {
            LLMChatMessageRole.System => new SystemChatMessage(message.Content),
            LLMChatMessageRole.Assistant => new AssistantChatMessage(message.Content),
            LLMChatMessageRole.User => new UserChatMessage(message.Content),
            _ => throw new InvalidOperationException(
                $"Unsupported LLMChatMessageRole: {message.Role}"
            )
        };
    }
}
