namespace Veeling.Core.Application;

public enum IntentResolutionOutcome
{
    Resolved,
    Clarification,
    Unsupported
}

public sealed record ResolvedCommand(
    IReadOnlyList<string> PathSegments,
    IReadOnlyDictionary<string, string?> Options,
    IReadOnlyList<string> Arguments,
    string Explanation,
    bool RequiresConfirmation,
    bool SupportsProjectFileOption,
    bool IsSuggestionOnly,
    string? SuggestionReason,
    string CanonicalInvocation);

public sealed record IntentResolutionResult(
    IntentResolutionOutcome Outcome,
    ResolvedCommand? Command,
    string Message);

public sealed record IntentParserRequest(
    string Intent,
    CommandCatalog Catalog,
    IntentParserProjectContext ProjectContext);

public sealed record IntentParserProjectContext(
    bool ProjectDetected,
    string? MasterLanguage,
    IReadOnlyList<string> Languages);

public sealed record IntentParserProviderSelection(string Provider, string? Model, DirectoryInfo ConfigDirectory);

public sealed record IntentParserResponse(
    string Outcome,
    string? Message,
    IntentParserCommandSpec? Command,
    IReadOnlyList<IntentParserCommandSpec>? Commands,
    string? Explanation,
    bool? RequiresConfirmation);

public sealed record IntentParserCommandSpec(
    IReadOnlyList<string> Path,
    IReadOnlyDictionary<string, string?>? Options,
    IReadOnlyList<string>? Arguments,
    bool? SuggestionOnly,
    string? SuggestionReason);
