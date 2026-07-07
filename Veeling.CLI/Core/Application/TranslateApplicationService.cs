using Veeling.CLI;
using Veeling.CLI.Exceptions;
using Veeling.Models;

namespace Veeling.Core.Application;

public sealed record TranslateSchemaResult(string SchemaName, string SourceLanguage, string TargetLanguage);

public sealed record TranslateSchemaWorkPlan(
    string SchemaName,
    Language SourceLanguage,
    Language TargetLanguage,
    IReadOnlyList<string> CandidateFields
);

public sealed record TranslateWorkPlan(
    IReadOnlyList<TranslateSchemaWorkPlan> SchemaPlans,
    int TotalCandidateFields
);

public sealed record TranslateCommandResult(
    string? Warning,
    IReadOnlyList<string> OutputLines,
    IReadOnlyList<TranslateSchemaResult> ProcessedSchemas,
    bool UsedProgressEvents = false
);

public sealed class TranslateApplicationService(TranslationJobFactory translationJobFactory)
{
    public TranslateWorkPlan BuildWorkPlan(
        Project project,
        Language from,
        IReadOnlyList<Language> toLanguages,
        bool changed)
    {
        List<TranslateSchemaWorkPlan> schemaPlans = [];

        foreach (VSchema schema in VSchema.ReadAll(project.Directory))
        {
            FileInfo fromDataFile = new(
                Path.Combine(
                    project.Directory.FullName,
                    Project.DataDirectoryName,
                    $"{schema.Model.Name}.{from.Code}.yaml"
                )
            );

            if (!fromDataFile.Exists)
            {
                throw new MissingSourceDataException(
                    $"Data file for source language '{from.Code}' does not exist for schema '{schema.Model.Name}', cannot continue. Missing file: {fromDataFile.FullName}"
                );
            }

            foreach (Language to in toLanguages)
            {
                TranslationJob job = translationJobFactory.Create(project, schema.Model.Name, from, to);
                job.IncludeChanged = changed && from.Equals(project.Model.MasterLanguage);

                IReadOnlyList<string> candidateFields = job.GetTranslationCandidateFields();
                schemaPlans.Add(new TranslateSchemaWorkPlan(schema.Model.Name, from, to, candidateFields));
            }
        }

        int totalCandidateFields = schemaPlans.Sum(plan => plan.CandidateFields.Count);
        return new TranslateWorkPlan(schemaPlans, totalCandidateFields);
    }

    public TranslateCommandResult Execute(
        Project project,
        Language from,
        IReadOnlyList<Language> toLanguages,
        bool dryRun,
        bool changed,
        Action<TranslateProgressEvent>? onProgress = null)
    {
        string? warning = null;

        if (!from.Equals(project.Model.MasterLanguage))
        {
            warning =
                $"Warning: source language '{from.Code}' is not the project master language '{project.Model.MasterLanguage.Code}'. Translation quality may suffer, and glossary terms are treated as soft hints.";
        }

        List<string> outputLines = [];
        List<TranslateSchemaResult> processed = [];

        TranslateWorkPlan workPlan = BuildWorkPlan(project, from, toLanguages, changed);
        int globalCompletedCount = 0;

        foreach (TranslateSchemaWorkPlan schemaPlan in workPlan.SchemaPlans)
        {
            outputLines.Add($"Processing schema '{schemaPlan.SchemaName}' ('{schemaPlan.SourceLanguage.Code}' -> '{schemaPlan.TargetLanguage.Code}')...");

            onProgress?.Invoke(new TranslateProgressEvent(
                Kind: TranslateProgressEventKind.SchemaStarted,
                SchemaName: schemaPlan.SchemaName,
                SourceLanguage: schemaPlan.SourceLanguage,
                TargetLanguage: schemaPlan.TargetLanguage,
                FieldName: null,
                TranslatedValuePreview: null,
                CompletedCount: globalCompletedCount,
                TotalCount: workPlan.TotalCandidateFields,
                SchemaCompletedCount: 0,
                SchemaTotalCount: schemaPlan.CandidateFields.Count,
                DryRun: dryRun
            ));

            TranslationJob job = translationJobFactory.Create(project, schemaPlan.SchemaName, schemaPlan.SourceLanguage, schemaPlan.TargetLanguage);
            job.DryRun = dryRun;
            job.IncludeChanged = changed && schemaPlan.SourceLanguage.Equals(project.Model.MasterLanguage);
            job.Output = outputLines.Add;
            job.OnProgress = onProgress;
            job.ConfigureProgressCounters(
                progressCompletedCount: globalCompletedCount,
                progressTotalCount: workPlan.TotalCandidateFields,
                schemaProgressCompletedCount: 0,
                schemaProgressTotalCount: schemaPlan.CandidateFields.Count);

            if (schemaPlan.CandidateFields.Count > 0)
            {
                job.Execute();
                globalCompletedCount += schemaPlan.CandidateFields.Count;
            }
            else
            {
                outputLines.Add("All fields are already translated, skipping.");

                onProgress?.Invoke(new TranslateProgressEvent(
                    Kind: TranslateProgressEventKind.SchemaSkipped,
                    SchemaName: schemaPlan.SchemaName,
                    SourceLanguage: schemaPlan.SourceLanguage,
                    TargetLanguage: schemaPlan.TargetLanguage,
                    FieldName: null,
                    TranslatedValuePreview: null,
                    CompletedCount: globalCompletedCount,
                    TotalCount: workPlan.TotalCandidateFields,
                    SchemaCompletedCount: 0,
                    SchemaTotalCount: schemaPlan.CandidateFields.Count,
                    DryRun: dryRun
                ));
            }

            processed.Add(new TranslateSchemaResult(schemaPlan.SchemaName, schemaPlan.SourceLanguage.Code, schemaPlan.TargetLanguage.Code));
        }

        return new TranslateCommandResult(warning, outputLines, processed, UsedProgressEvents: onProgress is not null);
    }
}
