using Anthropic;
using Anthropic.Models.Messages;
using System.Globalization;
using Veeling.CLI;

namespace Veeling.CLI.Providers;

public sealed class ClaudeProvider : ILLMProvider
{
    private const long DefaultMaxTokens = 4096;

    private readonly AnthropicClient client;
    private readonly string model;
    private readonly long maxTokens;

    public ClaudeProvider(VeelingConfig config, string? modelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        model = modelOverride
            ?? config.GetValue("claude_model")
            ?? throw new InvalidOperationException("Config key 'claude_model' is not set.");

        string apiKey = config.GetValue("claude_apikey")
            ?? throw new InvalidOperationException("Config key 'claude_apikey' is not set.");

        maxTokens = ParseMaxTokens(config.GetValue("claude_max_tokens"));

        client = new AnthropicClient
        {
            ApiKey = apiKey
        };
    }

    public LLMChatMessage Complete(params LLMChatMessage[] history)
    {
        string? systemInstruction = null;
        List<MessageParam> messages = [];

        foreach (LLMChatMessage message in history)
        {
            switch (message.Role)
            {
                case LLMChatMessageRole.System:
                    systemInstruction = systemInstruction is null
                        ? message.Content
                        : $"{systemInstruction}\n\n{message.Content}";
                    break;

                case LLMChatMessageRole.User:
                    messages.Add(new MessageParam
                    {
                        Role = Role.User,
                        Content = message.Content
                    });
                    break;

                case LLMChatMessageRole.Assistant:
                    messages.Add(new MessageParam
                    {
                        Role = Role.Assistant,
                        Content = message.Content
                    });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported LLMChatMessageRole: {message.Role}"
                    );
            }
        }

        if (messages.Count == 0)
        {
            throw new InvalidOperationException("No user or assistant messages were provided for Claude completion.");
        }

        MessageCreateParams request = new()
        {
            Model = model,
            MaxTokens = maxTokens,
            Messages = messages,
            System = string.IsNullOrWhiteSpace(systemInstruction)
                ? null
                : new MessageCreateParamsSystem(systemInstruction)
        };

        Message response = client.Messages
            .Create(request)
            .GetAwaiter()
            .GetResult();

        string text = ExtractText(response);

        return new LLMChatMessage(
            Role: LLMChatMessageRole.Assistant,
            Content: text
        );
    }

    private static long ParseMaxTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultMaxTokens;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) || parsed <= 0)
        {
            throw new InvalidOperationException(
                $"Config key 'claude_max_tokens' must be a positive integer, but was '{value}'."
            );
        }

        return parsed;
    }

    private static string ExtractText(Message response)
    {
        List<string> textParts = [];

        foreach (ContentBlock block in response.Content)
        {
            if (block.TryPickText(out TextBlock? textBlock) && !string.IsNullOrWhiteSpace(textBlock.Text))
            {
                textParts.Add(textBlock.Text);
            }
        }

        if (textParts.Count == 0)
        {
            throw new InvalidOperationException("Claude response did not contain text content.");
        }

        return string.Concat(textParts);
    }
}
