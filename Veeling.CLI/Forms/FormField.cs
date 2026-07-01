namespace Veeling.CLI.Forms;

public abstract class FormField(string prompt)
{
    protected Form? form;

    public string Prompt { get; } = prompt;

    public string? Value { get; set; }

    public Func<string?, string?>? Validate { get; set; }

    public virtual void Execute(Form form)
    {
        this.form = form;
    }
}
