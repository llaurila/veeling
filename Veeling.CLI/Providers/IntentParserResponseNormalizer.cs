using System.Text.Json;
using Veeling.Core.Application;

namespace Veeling.CLI.Providers;

internal static class IntentParserResponseNormalizer
{
    public static IntentParserResponse ParseAndNormalize(string payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Top-level parser payload must be a JSON object.");
        }

        root = UnwrapEnvelope(root);

        string outcome = ReadOutcome(root);
        string? message = ReadOptionalString(root, "message");
        string? explanation = ReadOptionalString(root, "explanation");
        bool? requiresConfirmation = ReadOptionalBoolean(root, "requiresConfirmation");

        IntentParserCommandSpec? command = ReadCommand(root, outcome);
        IReadOnlyList<IntentParserCommandSpec>? commands = ReadCommands(root);

        return new IntentParserResponse(
            Outcome: outcome,
            Message: message,
            Command: command,
            Commands: commands,
            Explanation: explanation,
            RequiresConfirmation: requiresConfirmation);
    }

    private static string ReadOutcome(JsonElement root)
    {
        if (TryGetProperty(root, out JsonElement outcomeElement, "outcome", "status", "result"))
        {
            string outcome = ReadRequiredString(outcomeElement, "outcome");
            return NormalizeOutcomeToken(outcome);
        }

        if (TryGetProperty(root, out _, "command", "commands", "resolvedCommand", "resolved_command"))
        {
            return "resolved";
        }

        throw new InvalidOperationException("Missing required field 'outcome'.");
    }

    private static IntentParserCommandSpec? ReadCommand(JsonElement root, string normalizedOutcome)
    {
        if (TryGetProperty(root, out JsonElement commandElement, "command", "resolvedCommand", "resolved_command"))
        {
            return ParseCommandSpec(commandElement, "command");
        }

        if (normalizedOutcome != "resolved")
        {
            return null;
        }

        if (TryGetProperty(root, out JsonElement commandTextElement, "canonicalCommand", "canonical_command", "commandText", "command_text"))
        {
            string commandText = ReadRequiredString(commandTextElement, "canonicalCommand");
            return ParseCommandString(commandText);
        }

        return null;
    }

    private static IReadOnlyList<IntentParserCommandSpec>? ReadCommands(JsonElement root)
    {
        if (!TryGetProperty(root, out JsonElement commandsElement, "commands"))
        {
            return null;
        }

        if (commandsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Field 'commands' must be an array.");
        }

        List<IntentParserCommandSpec> commands = [];
        int index = 0;
        foreach (JsonElement element in commandsElement.EnumerateArray())
        {
            commands.Add(ParseCommandSpec(element, $"commands[{index}]"));
            index++;
        }

        return commands;
    }

    private static IntentParserCommandSpec ParseCommandSpec(JsonElement commandElement, string fieldName)
    {
        if (commandElement.ValueKind == JsonValueKind.String)
        {
            return ParseCommandString(commandElement.GetString() ?? string.Empty);
        }

        if (commandElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Field '{fieldName}' must be an object or command string.");
        }

        IReadOnlyList<string> path = ReadPath(commandElement);
        IReadOnlyDictionary<string, string?>? options = ReadOptions(commandElement);
        IReadOnlyList<string>? arguments = ReadArguments(commandElement);
        bool? suggestionOnly = ReadOptionalBoolean(commandElement, "suggestionOnly", "suggestion_only");
        string? suggestionReason = ReadOptionalString(commandElement, "suggestionReason", "suggestion_reason");

        return new IntentParserCommandSpec(path, options, arguments, suggestionOnly, suggestionReason);
    }

    private static IntentParserCommandSpec ParseCommandString(string commandText)
    {
        EnsureNoChainingOrShellOperators(commandText);

        List<string> tokens = TokenizeCommand(commandText);
        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("Command string is empty.");
        }

        if (string.Equals(tokens[0], "veeling", StringComparison.OrdinalIgnoreCase))
        {
            tokens.RemoveAt(0);
        }

        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("Command string must include a Veeling command verb.");
        }

        List<string> path = [tokens[0]];
        Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);
        List<string> args = [];

        int index = 1;
        while (index < tokens.Count)
        {
            string token = tokens[index];

            if (token.StartsWith("-", StringComparison.Ordinal) && token.Length > 1)
            {
                string optionToken = token;

                if (optionToken.Contains('=', StringComparison.Ordinal))
                {
                    int separatorIndex = optionToken.IndexOf('=', StringComparison.Ordinal);
                    string key = optionToken[..separatorIndex];
                    string value = optionToken[(separatorIndex + 1)..];
                    options[key] = value;
                    index++;
                    continue;
                }

                if (index + 1 < tokens.Count && !tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[optionToken] = tokens[index + 1];
                    index += 2;
                }
                else
                {
                    options[optionToken] = null;
                    index++;
                }

                continue;
            }

            args.Add(token);
            index++;
        }

        return new IntentParserCommandSpec(path, options, args, null, null);
    }

    private static List<string> TokenizeCommand(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return [];
        }

        List<string> tokens = [];
        List<char> current = [];
        bool inQuotes = false;
        bool escape = false;

        foreach (char ch in commandText)
        {
            if (escape)
            {
                current.Add(ch);
                escape = false;
                continue;
            }

            if (ch == '\\' && inQuotes)
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Count > 0)
                {
                    tokens.Add(new string([.. current]));
                    current.Clear();
                }

                continue;
            }

            current.Add(ch);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("Command string contains an unmatched quote.");
        }

        if (current.Count > 0)
        {
            tokens.Add(new string([.. current]));
        }

        return tokens;
    }

    private static IReadOnlyList<string> ReadPath(JsonElement commandElement)
    {
        if (TryGetProperty(commandElement, out JsonElement pathElement,
            "path", "pathSegments", "path_segments", "commandPath", "command_path"))
        {
            if (pathElement.ValueKind == JsonValueKind.String)
            {
                List<string> split = pathElement
                    .GetString()!
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (split.Count == 0)
                {
                    throw new InvalidOperationException("Command path string is empty.");
                }

                return split;
            }

            if (pathElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Command path must be an array or string.");
            }

            List<string> path = [];
            foreach (JsonElement segmentElement in pathElement.EnumerateArray())
            {
                path.Add(ReadRequiredString(segmentElement, "path[]"));
            }

            if (path.Count == 0)
            {
                throw new InvalidOperationException("Command path is empty.");
            }

            return path;
        }

        if (TryGetProperty(commandElement, out JsonElement nameElement, "name", "verb", "command", "commandName", "command_name"))
        {
            string value = ReadRequiredString(nameElement, "name");
            List<string> split = value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (split.Count == 0)
            {
                throw new InvalidOperationException("Command name is empty.");
            }

            return split;
        }

        throw new InvalidOperationException("Command object is missing required path/name field.");
    }

    private static JsonElement UnwrapEnvelope(JsonElement root)
    {
        if (TryGetProperty(root, out JsonElement dataElement, "data", "response", "result", "payload")
            && dataElement.ValueKind == JsonValueKind.Object
            && LooksLikeResponseObject(dataElement))
        {
            return dataElement;
        }

        return root;
    }

    private static bool LooksLikeResponseObject(JsonElement element)
    {
        return TryGetProperty(element, out _, "outcome", "status", "result")
            || TryGetProperty(element, out _, "command", "commands", "resolvedCommand", "resolved_command", "canonicalCommand", "canonical_command");
    }

    private static IReadOnlyDictionary<string, string?>? ReadOptions(JsonElement commandElement)
    {
        if (!TryGetProperty(commandElement, out JsonElement optionsElement, "options", "flags", "parameters", "opts"))
        {
            return null;
        }

        if (optionsElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (optionsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Command options must be an object.");
        }

        Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty property in optionsElement.EnumerateObject())
        {
            string key = property.Name;
            JsonElement valueElement = property.Value;

            string normalizedKey = key.StartsWith("--", StringComparison.Ordinal)
                ? key
                : key.StartsWith("-", StringComparison.Ordinal) ? key : $"--{key}";
            normalizedKey = normalizedKey.Replace("_", "-", StringComparison.Ordinal);

            string? value = valueElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => null,
                JsonValueKind.False => null,
                JsonValueKind.String => valueElement.GetString(),
                JsonValueKind.Number => valueElement.GetRawText(),
                _ => throw new InvalidOperationException($"Option '{key}' has unsupported value type '{valueElement.ValueKind}'.")
            };

            if (valueElement.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            options[normalizedKey] = value;
        }

        return options;
    }

    private static void EnsureNoChainingOrShellOperators(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return;
        }

        if (commandText.Contains("&&", StringComparison.Ordinal)
            || commandText.Contains("||", StringComparison.Ordinal)
            || commandText.Contains(";", StringComparison.Ordinal)
            || commandText.Contains("|", StringComparison.Ordinal)
            || commandText.Contains("\n", StringComparison.Ordinal)
            || commandText.Contains("\r", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Command string contains shell chaining/operators, which is not allowed.");
        }
    }

    private static IReadOnlyList<string>? ReadArguments(JsonElement commandElement)
    {
        if (!TryGetProperty(commandElement, out JsonElement argumentsElement, "arguments", "args"))
        {
            return null;
        }

        if (argumentsElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (argumentsElement.ValueKind == JsonValueKind.String)
        {
            string raw = argumentsElement.GetString() ?? string.Empty;
            return raw.Length == 0 ? [] : [raw];
        }

        if (argumentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Command arguments must be an array or string.");
        }

        List<string> args = [];
        foreach (JsonElement argumentElement in argumentsElement.EnumerateArray())
        {
            args.Add(ReadRequiredString(argumentElement, "arguments[]"));
        }

        return args;
    }

    private static string NormalizeOutcomeToken(string rawOutcome)
    {
        string token = rawOutcome.Trim().ToLowerInvariant();
        return token switch
        {
            "resolved" => "resolved",
            "resolve" => "resolved",
            "success" => "resolved",
            "ok" => "resolved",
            "clarification" => "clarification",
            "clarify" => "clarification",
            "needs_clarification" => "clarification",
            "need_clarification" => "clarification",
            "unsupported" => "unsupported",
            "reject" => "unsupported",
            "error" => "unsupported",
            _ => token
        };
    }

    private static string ReadRequiredString(JsonElement element, string fieldName)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Field '{fieldName}' must be a string.");
        }

        string value = element.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Field '{fieldName}' must not be empty.");
        }

        return value.Trim();
    }

    private static string? ReadOptionalString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out JsonElement value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Field '{names[0]}' must be a string when provided.");
        }

        string text = value.GetString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool? ReadOptionalBoolean(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out JsonElement value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => ParseBooleanString(value.GetString(), names[0]),
            _ => throw new InvalidOperationException($"Field '{names[0]}' must be a boolean when provided.")
        };
    }

    private static bool ParseBooleanString(string? value, string fieldName)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException($"Field '{fieldName}' must be 'true' or 'false' when provided as string.")
        };
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        foreach (string name in names)
        {
            if (TryGetPropertyCaseInsensitive(root, name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
