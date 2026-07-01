using Google.GenAI;
using Google.GenAI.Types;
using Veeling.CLI;

namespace Veeling.CLI.Providers;

public sealed class GeminiProvider : ILLMProvider
{
    private readonly Client client;
    private readonly string model;

    public GeminiProvider(VeelingConfig config, string? modelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        model = modelOverride
            ?? config.GetValue("gemini_model")
            ?? throw new InvalidOperationException("Config key 'gemini_model' is not set.");

        string apiKey = config.GetValue("gemini_apikey")
            ?? throw new InvalidOperationException("Config key 'gemini_apikey' is not set.");

        client = new Client(vertexAI: false, apiKey: apiKey);
    }

    public LLMChatMessage Complete(params LLMChatMessage[] history)
    {
        string? systemInstruction = null;
        List<Content> contents = [];

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
                    contents.Add(new Content
                    {
                        Role = "user",
                        Parts = [Part.FromText(message.Content)]
                    });
                    break;

                case LLMChatMessageRole.Assistant:
                    contents.Add(new Content
                    {
                        Role = "model",
                        Parts = [Part.FromText(message.Content)]
                    });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported LLMChatMessageRole: {message.Role}"
                    );
            }
        }

        if (contents.Count == 0)
        {
            throw new InvalidOperationException("No user or assistant messages were provided for Gemini completion.");
        }

        GenerateContentConfig config = new()
        {
            ResponseMimeType = "application/json"
        };

        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            config.SystemInstruction = new Content
            {
                Parts = [Part.FromText(systemInstruction)]
            };
        }

        GenerateContentResponse response = client.Models
            .GenerateContentAsync(model, contents, config)
            .GetAwaiter()
            .GetResult();

        string? text = response.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini response did not contain text content.");
        }

        return new LLMChatMessage(
            Role: LLMChatMessageRole.Assistant,
            Content: text
        );
    }
}
