namespace Veeling.CLI.Templating;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TemplateVariableAttribute : Attribute
{
    public TemplateVariableAttribute() { }

    public TemplateVariableAttribute(string name) => Name = name;

    public string? Name { get; }
}
