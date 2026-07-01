using System.CommandLine;
using Veeling.Core.Application;
using Veeling.CLI.Forms;
using Veeling.Models;

namespace Veeling.CLI.Commands;

public class InitCommand : ICliCommand
{
    private readonly InitApplicationService initApplicationService;

    public const int Exit_Success = 0;
    public const int Exit_Failure_InvalidInput = 1;
    public const int Exit_Cancelled = 3;

    private readonly Option<string> nameOption = new("--name")
    {
        Description = "Name of the project.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-n" }
    };

    private readonly Option<string> descriptionOption = new("--description")
    {
        Description = "Description of the project.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-d" }
    };

    private readonly Option<string> languagesOption = new("--languages")
    {
        Description = "Comma-separated list of project languages (2-letter language codes, i.e. 'en,fr,de').",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-l" }
    };

    private readonly Option<string> masterLanguageOption = new("--master-language")
    {
        Description = "Master language of the project (2-letter language code, i.e. 'en').",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-ml" }
    };

    private readonly Option<string> toneOption = new("--tone")
    {
        Description = "Tone of language.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-t" }
    };

    private readonly Option<string> formalityOption = new("--formality")
    {
        Description = "Formality of language.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-f" }
    };

    private readonly Option<string> audienceOption = new("--audience")
    {
        Description = "Audience of content.",
        Required = false,
        Arity = ArgumentArity.ExactlyOne,
        Aliases = { "-a" }
    };

    private readonly Option<bool> yesOption = new("--yes")
    {
        Description = "Do not prompt to confirm before scaffolding.",
        Required = false,
        Arity = ArgumentArity.Zero,
        Aliases = { "-y" }
    };

    public InitCommand(InitApplicationService initApplicationService)
    {
        this.initApplicationService = initApplicationService;

        Command = new Command("init", "Initialize a new Veeling project.");
        Command.Options.Add(nameOption);
        Command.Options.Add(descriptionOption);
        Command.Options.Add(languagesOption);
        Command.Options.Add(masterLanguageOption);
        Command.Options.Add(toneOption);
        Command.Options.Add(formalityOption);
        Command.Options.Add(audienceOption);
        Command.Options.Add(yesOption);
        Command.SetAction(Execute);
    }

    public Command Command { get; }

    private int Execute(ParseResult parseResult)
    {
        Form form = new();

        TextField projectNameField = CreateProjectNameField();
        TextField projectDescriptionField = CreateProjectDescriptionField();
        TextField languagesField = CreateLanguagesField();
        TextField masterLanguageField = CreateMasterLanguageField();
        SelectField toneField = CreateToneField();
        SelectField formalityField = CreateFormalityField();
        TextField audienceField = CreateAudienceField();

        form.AddField(projectNameField);
        form.AddField(projectDescriptionField);
        form.AddField(languagesField);
        form.AddField(masterLanguageField);
        form.AddField(toneField);
        form.AddField(formalityField);
        form.AddField(audienceField);

        projectNameField.Value = parseResult.GetValue(nameOption);
        projectDescriptionField.Value = parseResult.GetValue(descriptionOption);
        languagesField.Value = parseResult.GetValue(languagesOption);
        masterLanguageField.Value = parseResult.GetValue(masterLanguageOption);
        toneField.Value = parseResult.GetValue(toneOption);
        formalityField.Value = parseResult.GetValue(formalityOption);
        audienceField.Value = parseResult.GetValue(audienceOption);

        form.Execute();

        Language[] languages = [.. ParseLanguages(languagesField.Value)];
        string masterLanguage = masterLanguageField.Value!.ToLowerInvariant();

        if (!languages.Contains(masterLanguage))
        {
            Console.Error.WriteLine($"Master language '{masterLanguage}' must be included in the project languages.");
            return Exit_Failure_InvalidInput;
        }

        ProjectModel model = new()
        {
            Name = projectNameField.Value!,
            Description = projectDescriptionField.Value!,
            Languages = languages,
            MasterLanguage = masterLanguage,
            Style = new()
            {
                Tone = Enum.Parse<Tone>(toneField.Value!, true),
                Formality = Enum.Parse<Formality>(formalityField.Value!, true),
                Audience = audienceField.Value!
            }
        };

        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.WriteLine($"- Name: {model.Name}");
        Console.WriteLine($"- Description: {Util.LimitString(EscapeLineBreaks(model.Description), 40)}");
        Console.WriteLine($"- Languages: {string.Join(", ", model.Languages.Select(l => Language.GetName(l)))}");
        Console.WriteLine($"- Master Language: {Language.GetName(model.MasterLanguage)}");
        Console.WriteLine("- Style:");
        Console.WriteLine($"  - Tone: {model.Style.Tone.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  - Formality: {model.Style.Formality.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  - Audience: {model.Style.Audience}");
        Console.WriteLine();

        DirectoryInfo projectRoot = new(
            Path.Combine(Directory.GetCurrentDirectory(), projectNameField.Value!.Trim())
        );

        if (!parseResult.GetValue(yesOption))
        {
            Console.WriteLine($"About to create directory {projectRoot.FullName}");
            Console.Write("Proceed? [y/N] ");

            string? response = Console.ReadLine();
            if (response == null || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                return Exit_Cancelled;
            }
        }

        InitProjectResult result = initApplicationService.Execute(new InitProjectRequest(projectRoot, model));

        foreach (string line in result.OutputLines)
        {
            Console.WriteLine(line);
        }

        foreach (string line in result.ErrorLines)
        {
            Console.Error.WriteLine(line);
        }

        return result.Success ? Exit_Success : Exit_Failure_InvalidInput;
    }

    private static TextField CreateProjectNameField()
    {
        return new TextField("Project name")
        {
            Validate = v => VSchema.IdentifierRegex.IsMatch(v ?? string.Empty) ? null
                : "Invalid name: " + VSchema.IdentifierPatternDescription
        };
    }

    private static TextField CreateProjectDescriptionField()
    {
        return new TextField("Description")
        {
            LongInput = true,
            Validate = v => !string.IsNullOrWhiteSpace(v) ? null
                : "Project description cannot be empty."
        };
    }

    private static TextField CreateLanguagesField()
    {
        return new TextField("Languages (comma-separated 2-letter language codes, i.e. 'en,fr,de')")
        {
            Validate = v =>
            {
                if (string.IsNullOrWhiteSpace(v))
                {
                    return "At least one language must be specified.";
                }

                Language[] langs = [.. ParseLanguages(v, emitWarnings: false)];
                return langs.Length > 0 ? null : "No valid language codes were provided.";
            }
        };
    }

    private static TextField CreateMasterLanguageField()
    {
        return new TextField("Master language (2-letter language code, i.e. 'en')")
        {
            Validate = v => Language.IsSupportedLanguage(v?.ToLowerInvariant() ?? string.Empty) ? null
                : "Unknown language code."
        };
    }

    private static SelectField CreateToneField()
    {
        return new SelectField(
            "Tone",
            [.. Enum.GetNames<Tone>().Select(t => t.ToLowerInvariant())]
        );
    }

    private static SelectField CreateFormalityField()
    {
        return new SelectField(
            "Formality",
            [.. Enum.GetNames<Formality>().Select(f => f.ToLowerInvariant())]
        );
    }

    private static TextField CreateAudienceField()
    {
        return new TextField("Audience (press Enter for 'general')")
        {
            DefaultValue = "general",
            Validate = v => !string.IsNullOrWhiteSpace(v) ? null
                : "Audience cannot be empty."
        };
    }

    private static string EscapeLineBreaks(string input)
    {
        return input.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static IEnumerable<Language> ParseLanguages(string? input, bool emitWarnings = true)
    {
        if (string.IsNullOrWhiteSpace(input)) yield break;

        string[] codes = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string code in codes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string normalizedCode = code.ToLowerInvariant();

            if (Language.IsSupportedLanguage(normalizedCode))
            {
                yield return normalizedCode;
            }
            else if (emitWarnings)
            {
                Console.WriteLine($"Warning: Unknown language code '{code}' - skipping.");
            }
        }
    }
}
