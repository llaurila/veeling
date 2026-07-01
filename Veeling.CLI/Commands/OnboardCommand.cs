using System.CommandLine;
using Veeling.CLI.Forms;
using Veeling.Core.Application;

namespace Veeling.CLI.Commands;

public sealed class OnboardCommand(OnboardingApplicationService onboardingApplicationService) : ICliCommand
{
    private static readonly string[] Providers = ["openai", "gemini", "claude"];

    private static readonly IReadOnlyDictionary<string, string[]> ModelOptions = new Dictionary<string, string[]>
    {
        ["openai"] = ["gpt-4.1-mini", "gpt-4.1", "gpt-4o-mini", "Other"],
        ["gemini"] = ["gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash", "Other"],
        ["claude"] = ["claude-sonnet-4-5", "claude-opus-4-1", "claude-3-7-sonnet-latest", "Other"]
    };

    public Command Command { get; } = BuildCommand(onboardingApplicationService);

    private static Command BuildCommand(OnboardingApplicationService onboardingApplicationService)
    {
        Command command = new("onboard", "Guided AI onboarding for provider setup and verification.");
        command.SetAction(parseResult => Execute(onboardingApplicationService));
        return command;
    }

    private static int Execute(OnboardingApplicationService onboardingApplicationService)
    {
        string provider = PromptProvider();
        string model = PromptModel(provider);
        string apiKey = PromptApiKey(provider);
        int? claudeMaxTokens = provider == "claude" ? PromptClaudeMaxTokens() : null;

        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.WriteLine($"- Provider: {provider}");
        Console.WriteLine($"- Model: {model}");
        Console.WriteLine("- API key: [redacted]");
        if (claudeMaxTokens.HasValue)
        {
            Console.WriteLine($"- Claude max tokens: {claudeMaxTokens.Value}");
        }

        Console.WriteLine();

        OnboardingCommandResult result = onboardingApplicationService.Execute(
            new OnboardingCommandRequest(provider, model, apiKey, claudeMaxTokens)
        );

        foreach (string line in result.OutputLines)
        {
            Console.WriteLine(line);
        }

        if (result.ErrorLines.Count == 0)
        {
            return 0;
        }

        foreach (string line in result.ErrorLines)
        {
            Console.Error.WriteLine(line);
        }

        return 1;
    }

    private static string PromptProvider()
    {
        Form form = new();
        SelectField providerField = new("Choose AI provider", Providers);
        form.AddField(providerField);
        form.Execute();

        return providerField.Value!;
    }

    private static string PromptModel(string provider)
    {
        Form form = new();
        SelectField modelField = new("Select model", ModelOptions[provider]);
        form.AddField(modelField);
        form.Execute();

        if (!string.Equals(modelField.Value, "Other", StringComparison.Ordinal))
        {
            return modelField.Value!;
        }

        TextField customModelField = new("Model name")
        {
            Validate = v => !string.IsNullOrWhiteSpace(v)
                ? null
                : "Model name cannot be empty."
        };

        Form customModelForm = new();
        customModelForm.AddField(customModelField);
        customModelForm.Execute();
        return customModelField.Value!;
    }

    private static string PromptApiKey(string provider)
    {
        TextField apiKeyField = new($"Enter {provider} API key")
        {
            Validate = v => !string.IsNullOrWhiteSpace(v)
                ? null
                : "API key cannot be empty."
        };

        Form form = new();
        form.AddField(apiKeyField);
        form.Execute();
        return apiKeyField.Value!;
    }

    private static int PromptClaudeMaxTokens()
    {
        TextField maxTokensField = new("Claude max tokens")
        {
            DefaultValue = "4096",
            Validate = v => int.TryParse(v, out int parsed) && parsed > 0
                ? null
                : "Claude max tokens must be a positive integer."
        };

        Form form = new();
        form.AddField(maxTokensField);
        form.Execute();
        return int.Parse(maxTokensField.Value!);
    }
}
