using System.CommandLine;
using Veeling.Models;

namespace Veeling.CLI.Commands;

public static class CommandUtils
{
    public const string DefaultProjectFileName = "Project.yaml";

    public static Option<string> CreateProjectFileOption()
    {
        return new Option<string>("--project-file")
        {
            Description = $"Path to the project file to use (default: {DefaultProjectFileName})",
            Required = false,
            Arity = ArgumentArity.ExactlyOne,
            Aliases = { "-p" }
        };
    }

    public static Argument<string> CreateRecordSpecArgument()
    {
        return new Argument<string>("record-spec")
        {
            Description = "Record specification in the format '<schema>.<field>:<language>'.",
            Arity = ArgumentArity.ExactlyOne
        };
    }

    public static Argument<string?> CreateOptionalSelectorArgument()
    {
        return new Argument<string?>("selector")
        {
            Description = "Optional selector in the format '<schema>.<field>:<language>' (omit to export all: '*.*:*').",
            Arity = ArgumentArity.ZeroOrOne
        };
    }

    public static Project? GetProject(ParseResult parseResult, Option<string> projectFileOption)
    {
        FileInfo projectFile = GetProjectFileInfo(parseResult, projectFileOption);

        if (!projectFile.Exists)
        {
            Console.Error.WriteLine($"Project file not found: {projectFile.FullName}");
            return null;
        }

        return new Project(projectFile);
    }

    public static FileInfo GetProjectFileInfo(ParseResult parseResult, Option<string> projectFileOption)
    {
        string? path = parseResult.GetValue(projectFileOption);
        if (path is null) return GetDefaultProjectFileInfo();
        return new FileInfo(path);
    }

    public static DirectoryInfo GetProjectDirectory(ParseResult parseResult, Option<string> projectFileOption)
    {
        FileInfo projectFile = GetProjectFileInfo(parseResult, projectFileOption);
        return projectFile.Directory ?? new DirectoryInfo(Directory.GetCurrentDirectory());
    }

    public static FileInfo GetDefaultProjectFileInfo()
    {
        return new FileInfo(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                DefaultProjectFileName
            )
        );
    }

    public static Language[] ParseTargetLanguages(string languageSpec, Project project)
    {
        if (languageSpec == "*")
        {
            return project.Model.Languages;
        }

        try
        {
            return [.. languageSpec
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(code => new Language(code))];
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return [];
        }
    }

    public static bool TryGetRecordSpec(ParseResult parseResult, Argument<string> recordSpecArgument, out RecordFilter recordSpec)
    {
        try
        {
            recordSpec = parseResult.GetValue(recordSpecArgument)!;
            return true;
        }
        catch (ArgumentException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.Message);
            Console.ResetColor();
            recordSpec = null!;
            return false;
        }
    }
}
