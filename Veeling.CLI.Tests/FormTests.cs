using Veeling.CLI.Forms;

namespace Veeling.CLI.Tests;

public class FormTests
{
    private readonly Form form = new();

    private readonly TextField field1 = new("Field 1")
    {
        Validate = v => v?.Length == 2 ? null : "Length must be 2"
    };

    private readonly TextField field2 = new("Field 2")
    {
        LongInput = true
    };

    private readonly TextField field3 = new("Field 3")
    {
        Value = "has_value"
    };

    [Fact]
    public void IfFieldHasValue_DoNothing()
    {
        form.Output = s => throw new InvalidOperationException("Output should not be called");
        form.AddField(field3);
        form.Execute();
        Assert.Equal("has_value", field3.Value);
    }

    [Fact]
    public void ValidateField_WhenInputIsValid_SetsValue()
    {
        var outputs = new List<string>();
        var inputs = new Queue<string?>(["ab"]);

        form.Output = outputs.Add;
        form.Input = () => inputs.Dequeue();

        form.AddField(field1);
        form.Execute();

        Assert.Equal("ab", field1.Value);
        Assert.Single(outputs);
        Assert.Equal("Field 1: ", outputs[0]);
    }

    [Fact]
    public void ValidateField_WhenInputIsInvalid_RePromptsUntilValid()
    {
        var outputs = new List<string>();
        var inputs = new Queue<string?>(["a", "ab"]);

        form.Output = outputs.Add;
        form.Input = () => inputs.Dequeue();

        form.AddField(field1);
        form.Execute();

        Assert.Equal("ab", field1.Value);
        Assert.Equal(
            ["Field 1: ", "Length must be 2" + Environment.NewLine, "Field 1: "],
            outputs);
    }

    [Fact]
    public void LongInput_ReadsMultilineInputUntilDot()
    {
        var outputs = new List<string>();
        var inputs = new Queue<string?>(["first line", "second line", "."]);

        form.Output = outputs.Add;
        form.Input = () => inputs.Dequeue();

        form.AddField(field2);
        form.Execute();

        Assert.Equal($"first line{Environment.NewLine}second line", field2.Value);
        Assert.Single(outputs);
        Assert.Equal($"Field 2 (end with a single '.' on a line):{Environment.NewLine}", outputs[0]);
    }

    [Fact]
    public void SelectField_WhenInputIsValid_SetsValue()
    {
        var outputs = new List<string>();
        using var input = new StringReader("2" + Environment.NewLine);
        Console.SetIn(input);

        form.Output = outputs.Add;

        var selectField = new SelectField("Pick", ["One", "Two"]);
        form.AddField(selectField);
        form.Execute();

        Assert.Equal("Two", selectField.Value);
        Assert.Equal(
            [
                "Pick:\n",
                "1. One\n",
                "2. Two\n",
                "Your choice: "
            ],
            outputs);
    }

    [Fact]
    public void SelectField_WhenInputIsInvalid_RePromptsUntilValid()
    {
        var outputs = new List<string>();
        using var input = new StringReader($"3{Environment.NewLine}1{Environment.NewLine}");
        Console.SetIn(input);

        form.Output = outputs.Add;

        var selectField = new SelectField("Pick", ["One", "Two"]);
        form.AddField(selectField);
        form.Execute();

        Assert.Equal("One", selectField.Value);
        Assert.Equal(
            [
                "Pick:\n",
                "1. One\n",
                "2. Two\n",
                "Your choice: ",
                "Invalid input. Please enter a valid number.\n",
                "Pick:\n",
                "1. One\n",
                "2. Two\n",
                "Your choice: "
            ],
            outputs);
    }
}
