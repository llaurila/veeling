using System.Text.Json;
using Veeling.CLI;
using Veeling.Core.Application;

namespace Veeling.CLI.Providers;

public sealed class ConfiguredIntentParserProvider(IGlobalConfigFileLocator globalConfigFileLocator) : IIntentParserProvider
{
    public IntentParserResponse Parse(IntentParserProviderSelection selection, IntentParserRequest request)
    {
        if (selection is null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        VeelingConfig config = new(selection.ConfigDirectory, globalConfigFileLocator);
        string providerType = selection.Provider.Trim().ToLowerInvariant();

        ILLMProvider provider = providerType switch
        {
            "openai" => new OpenAIProvider(config, selection.Model),
            "gemini" => new GeminiProvider(config, selection.Model),
            "claude" => new ClaudeProvider(config, selection.Model),
            _ => throw new InvalidOperationException(
                $"Unknown intent parser provider '{selection.Provider}'. Supported values: openai, gemini, claude.")
        };

        string systemPrompt = BuildSystemPrompt(request);
        string userPrompt = BuildUserPrompt(request.Intent, request.ProjectContext);

        LLMChatMessage response = provider.Complete(
            new LLMChatMessage(LLMChatMessageRole.System, systemPrompt),
            new LLMChatMessage(LLMChatMessageRole.User, userPrompt));

        string payload = IntentParserResponseJsonExtractor.ExtractSingleJsonObject(response.Content);

        try
        {
            return IntentParserResponseNormalizer.ParseAndNormalize(payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Intent parser response JSON payload is invalid. {BuildSafeJsonParseDetails(ex)}",
                ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Intent parser response does not match expected contract. {BuildSafeContractDetails(ex.Message)}", ex);
        }
    }

    private static string BuildSystemPrompt(IntentParserRequest request)
    {
        string catalog = JsonSerializer.Serialize(request.Catalog, new JsonSerializerOptions { WriteIndented = true });

        return $"""
            You map Veeling user intent to exactly one existing Veeling CLI command.
            Constraints:
            - Return strict JSON only.
            - Outcome must be one of: resolved, clarification, unsupported.
            - For resolved, return exactly one command in 'command'.
            - command may be either:
              a) an object with fields: path (array), options (object), arguments (array)
              b) a single canonical Veeling command string (for example: "config --global" or "veeling translate --to pt").
            - If command is an object, use field names path/options/arguments.
            - Options keys should be CLI flags (prefer --long-form), never natural-language labels.
            - Never return multiple commands or command chains.
            - Never return ai/ask as target command path.
            - Prefer clarification over guessing for ambiguous/high-risk requests.
            - Interactive/meta commands may be suggestion-only.

            Command catalog:
            {catalog}
            """;
    }

    private static string BuildUserPrompt(string intent, IntentParserProjectContext context)
    {
        string contextJson = JsonSerializer.Serialize(context);
        return $"Intent: {intent}\nProjectContext: {contextJson}";
    }

    private static string BuildSafeJsonParseDetails(JsonException exception)
    {
        string path = string.IsNullOrWhiteSpace(exception.Path) ? "$" : exception.Path;
        long line = exception.LineNumber ?? 0;
        long bytePosition = exception.BytePositionInLine ?? 0;
        return $"Path: {path}; line: {line}; byte: {bytePosition}.";
    }

    private static string BuildSafeContractDetails(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Response fields were not compatible with expected schema.";
        }

        string compact = message.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        const int maxLength = 220;
        return compact.Length <= maxLength
            ? compact
            : $"{compact[..maxLength]}…";
    }
}
