namespace Veeling.CLI.Forms;

public class SelectField(string prompt, string[] options) : FormField(prompt)
{
    public string[] Options { get; } = options;

    public bool AllowNone { get; set; } = false;

    public override void Execute(Form form)
    {
        base.Execute(form);

        while (true)
        {
            form.Output($"{Prompt}:\n");

            for (int i = 0; i < Options.Length; i++)
            {
                form.Output($"{i + 1}. {Options[i]}\n");
            }

            form.Output(GetChoicePrompt());
            string? input = form.Input();

            if (string.IsNullOrWhiteSpace(input) && AllowNone)
            {
                Value = null;
                break;
            }

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= Options.Length)
            {
                Value = Options[choice - 1];
                break;
            }
            else
            {
                form.Output("Invalid input. Please enter a valid number.\n");
            }
        }
    }

    private string GetChoicePrompt()
    {
        string prompt = "Your choice";

        if (AllowNone)
        {
            prompt += " (or Enter for none)";
        }

        return prompt + ": ";
    }
}
