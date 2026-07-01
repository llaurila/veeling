using Veeling.Models;

namespace Veeling.CLI.Tests;

public class ProjectTests
{
    [Fact]
    public void FromYaml_Valid()
    {
        const string yaml = @"
name: Sample Project

description: Foobar

languages:
  - en
  - fi

master_language: en

style:
  tone: playful
  formality: informal
  audience: general
";

        var schema = ProjectModel.FromYaml(yaml);

        Assert.Equal("Sample Project", schema.Name);
        Assert.Equal("Foobar", schema.Description);

        Assert.Equal(2, schema.Languages.Length);
        Assert.Contains(schema.Languages, l => l.Code == "en");
        Assert.Contains(schema.Languages, l => l.Code == "fi");
    }

    [Fact]
    public void FromYaml_MissingDescription_ThrowsException()
    {
        const string yaml = @"
name: Sample Project

languages:
  - en
  - fi

include:
  - module1";

        Assert.Throws<YamlDotNet.Core.YamlException>(() => ProjectModel.FromYaml(yaml));
    }
}
