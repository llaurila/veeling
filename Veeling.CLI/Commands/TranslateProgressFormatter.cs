using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

internal static class TranslateProgressFormatter
{
    public static string? Format(TranslateProgressEvent progressEvent)
    {
        return progressEvent.Kind switch
        {
            TranslateProgressEventKind.SchemaStarted =>
                $"Processing schema '{progressEvent.SchemaName}' ('{progressEvent.SourceLanguage.Code}' -> '{progressEvent.TargetLanguage.Code}')...",

            TranslateProgressEventKind.FieldTranslated =>
                $"[{progressEvent.CompletedCount}/{progressEvent.TotalCount}] Translated field {progressEvent.SchemaName}.{progressEvent.FieldName}: {progressEvent.TranslatedValuePreview ?? string.Empty}",

            TranslateProgressEventKind.SchemaSkipped =>
                "All fields are already translated, skipping.",

            TranslateProgressEventKind.SaveStarted =>
                null,

            TranslateProgressEventKind.SaveCompleted =>
                "Saving changes... ok",

            TranslateProgressEventKind.CompletedWithoutChanges =>
                "No changes.",

            _ => null
        };
    }
}
