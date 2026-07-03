using Veeling.CLI;
using Veeling.CLI.Exceptions;
using Veeling.Models;

namespace Veeling.Core.Application;

public sealed record TranslateSchemaResult(string SchemaName, string SourceLanguage, string TargetLanguage);

public sealed record TranslateCommandResult(
    string? Warning,
    IReadOnlyList<string> OutputLines,
    IReadOnlyList<TranslateSchemaResult> ProcessedSchemas
);

public sealed class TranslateApplicationService(TranslationJobFactory translationJobFactory)
{
    public TranslateCommandResult Execute(
        Project project,
        Language from,
        IReadOnlyList<Language> toLanguages,
        bool dryRun,
        bool changed)
    {
        string? warning = null;

        if (!from.Equals(project.Model.MasterLanguage))
        {
            warning =
                $"Warning: source language '{from.Code}' is not the project master language '{project.Model.MasterLanguage.Code}'. Translation quality may suffer, and glossary terms are treated as soft hints.";
        }

        List<string> outputLines = [];
        List<TranslateSchemaResult> processed = [];

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
                outputLines.Add($"Processing schema '{schema.Model.Name}' ('{from.Code}' -> '{to.Code}')...");

                TranslationJob job = translationJobFactory.Create(project, schema.Model.Name, from, to);
                job.DryRun = dryRun;
                job.IncludeChanged = changed && from.Equals(project.Model.MasterLanguage);
                job.Output = outputLines.Add;

                if (job.HasUntranslatedFields())
                {
                    job.Execute();
                }
                else
                {
                    outputLines.Add("All fields are already translated, skipping.");
                }

                processed.Add(new TranslateSchemaResult(schema.Model.Name, from.Code, to.Code));
            }
        }

        return new TranslateCommandResult(warning, outputLines, processed);
    }
}
