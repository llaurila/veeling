using Veeling.CLI.Providers;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests;

public sealed class ConfiguredIntentParserProviderTests
{
    [Fact]
    public void ExtractSingleJsonObject_RawValidJson_ReturnsJson()
    {
        const string input = "{\"outcome\":\"clarification\",\"message\":\"Need scope\"}";

        string actual = IntentParserResponseJsonExtractor.ExtractSingleJsonObject(input);

        Assert.Equal(input, actual);
    }

    [Fact]
    public void ExtractSingleJsonObject_FencedJson_ReturnsJsonPayload()
    {
        const string input = "```json\n{\"outcome\":\"clarification\",\"message\":\"Need scope\"}\n```";

        string actual = IntentParserResponseJsonExtractor.ExtractSingleJsonObject(input);

        Assert.Equal("{\"outcome\":\"clarification\",\"message\":\"Need scope\"}", actual);
    }

    [Fact]
    public void ExtractSingleJsonObject_ProseAndFencedJson_ReturnsJsonPayload()
    {
        const string input = "Here is the resolved intent:\n```json\n{\"outcome\":\"resolved\",\"message\":\"ok\",\"command\":{\"path\":[\"status\"],\"options\":{},\"arguments\":[]}}\n```\nUse this command.";

        string actual = IntentParserResponseJsonExtractor.ExtractSingleJsonObject(input);

        Assert.Equal("{\"outcome\":\"resolved\",\"message\":\"ok\",\"command\":{\"path\":[\"status\"],\"options\":{},\"arguments\":[]}}", actual);
    }

    [Fact]
    public void ExtractSingleJsonObject_InvalidNonJson_ThrowsClearError()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            IntentParserResponseJsonExtractor.ExtractSingleJsonObject("I cannot comply with this request."));

        Assert.Contains("must contain a JSON object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractSingleJsonObject_MultipleDifferentJsonObjects_ThrowsAmbiguousError()
    {
        const string input = "{\"outcome\":\"clarification\"}\n{\"outcome\":\"unsupported\"}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            IntentParserResponseJsonExtractor.ExtractSingleJsonObject(input));

        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_CommandAsString_ReturnsParsedCommandSpec()
    {
        const string payload = """
            {
              "outcome": "resolved",
              "command": "config --global"
            }
            """;

        IntentParserResponse response = IntentParserResponseNormalizer.ParseAndNormalize(payload);

        Assert.Equal("resolved", response.Outcome);
        Assert.NotNull(response.Command);
        Assert.Equal(["config"], response.Command!.Path);
        Assert.Equal(1, response.Command.Options!.Count);
        Assert.True(response.Command.Options.ContainsKey("--global"));
        Assert.Null(response.Command.Options["--global"]);
        Assert.Empty(response.Command.Arguments!);
    }

    [Fact]
    public void Normalize_CommandObjectSnakeCase_ReturnsCanonicalShape()
    {
        const string payload = """
            {
              "outcome": "resolved",
              "command": {
                "path_segments": ["translate"],
                "options": { "to": "pt", "dry_run": true },
                "args": []
              }
            }
            """;

        IntentParserResponse response = IntentParserResponseNormalizer.ParseAndNormalize(payload);

        Assert.Equal(["translate"], response.Command!.Path);
        Assert.Equal("pt", response.Command.Options!["--to"]);
        Assert.True(response.Command.Options.ContainsKey("--dry-run"));
        Assert.Null(response.Command.Options["--dry-run"]);
    }

    [Fact]
    public void Normalize_EnvelopeWithData_UnwrapsPayload()
    {
        const string payload = """
            {
              "id": "abc123",
              "data": {
                "status": "clarification",
                "message": "Which scope?"
              }
            }
            """;

        IntentParserResponse response = IntentParserResponseNormalizer.ParseAndNormalize(payload);

        Assert.Equal("clarification", response.Outcome);
        Assert.Equal("Which scope?", response.Message);
    }

    [Fact]
    public void Normalize_CommandStringWithChaining_RejectsFailClosed()
    {
        const string payload = """
            {
              "outcome": "resolved",
              "command": "config --global && status"
            }
            """;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            IntentParserResponseNormalizer.ParseAndNormalize(payload));

        Assert.Contains("chaining", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
