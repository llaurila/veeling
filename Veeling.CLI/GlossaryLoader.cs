using Veeling.CLI.Exceptions;
using Veeling.Models;
using YamlDotNet.Core;

namespace Veeling.CLI;

public class GlossaryLoader
{
    public GlossaryModel? Load(Project project, Language targetLanguage)
    {
        FileInfo glossaryFile = GetGlossaryFile(project, targetLanguage);

        if (!glossaryFile.Exists)
        {
            return null;
        }

        GlossaryModel glossary = ReadGlossary(glossaryFile);
        Validate(glossary, glossaryFile, targetLanguage);
        return glossary;
    }

    public static string GetGlossaryFileName(Language language)
    {
        return $"glossary.{language.Code}.yaml";
    }

    private static FileInfo GetGlossaryFile(Project project, Language targetLanguage)
    {
        return new FileInfo(
            Path.Combine(
                project.Directory.FullName,
                GetGlossaryFileName(targetLanguage)
            )
        );
    }

    private static GlossaryModel ReadGlossary(FileInfo glossaryFile)
    {
        string yaml;

        try
        {
            yaml = File.ReadAllText(glossaryFile.FullName);
        }
        catch (IOException ex)
        {
            throw new GlossaryValidationException(
                $"Failed to read glossary file '{glossaryFile.FullName}': {ex.Message}",
                ex
            );
        }

        try
        {
            return GlossaryModel.FromYaml(yaml);
        }
        catch (YamlException ex)
        {
            throw new GlossaryValidationException(
                $"Invalid glossary YAML in '{glossaryFile.FullName}': {ex.Message}",
                ex
            );
        }
        catch (LanguageException ex)
        {
            throw new GlossaryValidationException(
                $"Invalid glossary language in '{glossaryFile.FullName}': {ex.Message}",
                ex
            );
        }
    }

    private static void Validate(GlossaryModel glossary, FileInfo glossaryFile, Language targetLanguage)
    {
        if (!glossary.Language.Equals(targetLanguage))
        {
            throw new GlossaryValidationException(
                $"Glossary file '{glossaryFile.Name}' targets language '{targetLanguage.Code}', but contains language '{glossary.Language.Code}'."
            );
        }

        for (int i = 0; i < glossary.Entries.Length; i++)
        {
            GlossaryEntryModel entry = glossary.Entries[i];
            string entryPath = $"{glossaryFile.Name} entries[{i}]";

            if (entry.ForbiddenVariants is null)
            {
                throw new GlossaryValidationException(
                    $"{entryPath}: 'forbidden_variants' cannot be null."
                );
            }

            if (entry.AppliesTo is null)
            {
                throw new GlossaryValidationException(
                    $"{entryPath}: 'applies_to' cannot be null."
                );
            }

            if (string.IsNullOrWhiteSpace(entry.Term))
            {
                throw new GlossaryValidationException($"{entryPath}: 'term' is required.");
            }

            if (string.IsNullOrWhiteSpace(entry.Translation))
            {
                throw new GlossaryValidationException($"{entryPath}: 'translation' is required.");
            }

            if (entry.AppliesTo.Length == 0)
            {
                throw new GlossaryValidationException(
                    $"{entryPath}: 'applies_to' cannot be an empty list."
                );
            }

            for (int forbiddenIndex = 0; forbiddenIndex < entry.ForbiddenVariants.Length; forbiddenIndex++)
            {
                string forbiddenVariant = entry.ForbiddenVariants[forbiddenIndex];
                if (string.IsNullOrWhiteSpace(forbiddenVariant))
                {
                    throw new GlossaryValidationException(
                        $"{entryPath}: 'forbidden_variants[{forbiddenIndex}]' cannot be empty."
                    );
                }
            }
        }
    }
}
