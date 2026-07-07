using Veeling.CLI.Commands;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Tests.Commands;

public sealed class TranslateProgressFormatterTests
{
    [Fact]
    public void Format_FieldTranslated_UsesBracketProgressAndSchemaFieldPath()
    {
        TranslateProgressEvent evt = new(
            Kind: TranslateProgressEventKind.FieldTranslated,
            SchemaName: "Schema1",
            SourceLanguage: new Language("en"),
            TargetLanguage: new Language("fi"),
            FieldName: "Field1",
            TranslatedValuePreview: "Hei",
            CompletedCount: 3,
            TotalCount: 7,
            SchemaCompletedCount: 2,
            SchemaTotalCount: 4,
            DryRun: true);

        string? line = TranslateProgressFormatter.Format(evt);

        Assert.Equal("[3/7] Translated field Schema1.Field1: Hei", line);
    }

    [Fact]
    public void Format_SaveCompleted_RendersLegacyWording()
    {
        TranslateProgressEvent evt = new(
            Kind: TranslateProgressEventKind.SaveCompleted,
            SchemaName: "Schema1",
            SourceLanguage: new Language("en"),
            TargetLanguage: new Language("fi"),
            FieldName: null,
            TranslatedValuePreview: null,
            CompletedCount: 1,
            TotalCount: 1,
            SchemaCompletedCount: 1,
            SchemaTotalCount: 1,
            DryRun: false);

        string? line = TranslateProgressFormatter.Format(evt);

        Assert.Equal("Saving changes... ok", line);
    }
}
