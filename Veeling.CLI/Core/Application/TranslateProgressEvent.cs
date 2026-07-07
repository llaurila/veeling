using Veeling.Models;

namespace Veeling.Core.Application;

public enum TranslateProgressEventKind
{
    SchemaStarted,
    FieldTranslated,
    SchemaSkipped,
    SaveStarted,
    SaveCompleted,
    CompletedWithoutChanges
}

public sealed record TranslateProgressEvent(
    TranslateProgressEventKind Kind,
    string SchemaName,
    Language SourceLanguage,
    Language TargetLanguage,
    string? FieldName,
    string? TranslatedValuePreview,
    int CompletedCount,
    int TotalCount,
    int? SchemaCompletedCount,
    int? SchemaTotalCount,
    bool DryRun
);
