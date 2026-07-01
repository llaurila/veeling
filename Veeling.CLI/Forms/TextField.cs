namespace Veeling.CLI.Forms;

public class TextField(string prompt) : FormField(prompt)
{
    public string? DefaultValue { get; set; }

    public bool LongInput { get; set; } = false;

    public override void Execute(Form form)
    {
        base.Execute(form);

        string prompt = GetPrompt();
        form.Output(prompt);
        string? input = ReadInput();

        if (Validate is not null)
        {
            string? validationResult = Validate(input);
            while (validationResult is not null)
            {
                form.Output(validationResult + Environment.NewLine);

                form.Output(prompt);
                input = ReadInput();

                validationResult = Validate(input);
            }
        }

        Value = input;
    }

    private string GetPrompt()
    {
        return LongInput ? $"{Prompt} (end with a single '.' on a line):{Environment.NewLine}" : $"{Prompt}: ";
    }

    private string? ReadInput()
    {
        string? input = LongInput ? ReadLongInput() : form?.Input();
        if (string.IsNullOrWhiteSpace(input))
        {
            return DefaultValue;
        }
        return input;
    }

    private string? ReadLongInput()
    {
        List<string> lines = [];
        while (true)
        {
            string? line = form?.Input();
            if (line == ".")
            {
                break;
            }
            lines.Add(line ?? string.Empty);
        }
        return string.Join(Environment.NewLine, lines);
    }
}
