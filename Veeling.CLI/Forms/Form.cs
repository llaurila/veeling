namespace Veeling.CLI.Forms;

public class Form
{
    private readonly List<FormField> fields = [];

    public Func<string?> Input = () => Console.ReadLine();

    public Action<string> Output = (s) => Console.Write(s);

    public void AddField(FormField field)
    {
        fields.Add(field);
    }

    public void Execute()
    {
        foreach (var field in fields)
        {
            if (field.Value is null)
            {
                field.Execute(this);
            }
        }
    }
}
