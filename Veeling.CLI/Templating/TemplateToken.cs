namespace Veeling.CLI.Templating;

public readonly struct TemplateToken(string text, bool isVariable)
{
    public string Value { get; } = text;

    public bool IsVariable { get; } = isVariable;
}
