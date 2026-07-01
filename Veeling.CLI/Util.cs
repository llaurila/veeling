using Veeling.CLI.Templating;

namespace Veeling.CLI;

public static class Util
{
    public static readonly StringTemplate SystemPromptTemplate = new(
        ReadEmbeddedResource("Veeling.CLI.AI.SystemPrompt.txt")
    );

    public static readonly StringTemplate TranslatePromptTemplate = new(
        ReadEmbeddedResource("Veeling.CLI.AI.TranslatePrompt.txt")
    );

    public static string ReadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(Util).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string LimitString(string input, int maxLength, string ellipsis = "…")
    {
        if (input.Length <= maxLength) return input;
        if (maxLength <= ellipsis.Length) return ellipsis[..maxLength];
        return input[..(maxLength - ellipsis.Length)] + ellipsis;
    }

    public static string SpeakFileSize(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes), "File size cannot be negative.");

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
