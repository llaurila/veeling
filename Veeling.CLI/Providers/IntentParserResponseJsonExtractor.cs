using System.Text;

namespace Veeling.CLI.Providers;

internal static class IntentParserResponseJsonExtractor
{
    public static string ExtractSingleJsonObject(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            throw new InvalidOperationException("Intent parser response must contain JSON.");
        }

        List<string> candidates = [];
        candidates.AddRange(ExtractFencedJsonBlocks(responseContent));
        candidates.AddRange(ExtractTopLevelJsonObjects(responseContent));

        List<string> uniqueCandidates = candidates
            .Select(candidate => candidate.Trim())
            .Where(candidate => candidate.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (uniqueCandidates.Count == 0)
        {
            throw new InvalidOperationException("Intent parser response must contain a JSON object.");
        }

        if (uniqueCandidates.Count > 1)
        {
            throw new InvalidOperationException("Intent parser response is ambiguous: multiple JSON objects found.");
        }

        return uniqueCandidates[0];
    }

    private static IEnumerable<string> ExtractFencedJsonBlocks(string responseContent)
    {
        const string fence = "```";
        int cursor = 0;

        while (true)
        {
            int open = responseContent.IndexOf(fence, cursor, StringComparison.Ordinal);
            if (open < 0)
            {
                yield break;
            }

            int languageStart = open + fence.Length;
            int lineEnd = responseContent.IndexOf('\n', languageStart);
            if (lineEnd < 0)
            {
                yield break;
            }

            string infoString = responseContent[languageStart..lineEnd].Trim();
            int close = responseContent.IndexOf(fence, lineEnd + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                yield break;
            }

            if (IsJsonFence(infoString))
            {
                string content = responseContent[(lineEnd + 1)..close].Trim();
                if (content.Length > 0)
                {
                    yield return content;
                }
            }

            cursor = close + fence.Length;
        }
    }

    private static bool IsJsonFence(string infoString)
    {
        if (string.IsNullOrWhiteSpace(infoString))
        {
            return false;
        }

        string normalized = infoString.Trim().ToLowerInvariant();
        if (normalized.StartsWith("json", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> ExtractTopLevelJsonObjects(string responseContent)
    {
        bool inString = false;
        bool escape = false;
        int depth = 0;
        int objectStart = -1;

        for (int index = 0; index < responseContent.Length; index++)
        {
            char current = responseContent[index];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '{')
            {
                if (depth == 0)
                {
                    objectStart = index;
                }

                depth++;
                continue;
            }

            if (current == '}' && depth > 0)
            {
                depth--;
                if (depth == 0 && objectStart >= 0)
                {
                    yield return responseContent[objectStart..(index + 1)];
                    objectStart = -1;
                }
            }
        }
    }
}
