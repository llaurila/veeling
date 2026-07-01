using Veeling.Models.Schema;

namespace Veeling.CLI.Tests;

public class SchemaTests
{
    [Fact]
    public void FromYaml_Valid()
    {
        const string yaml = @"
name: Sample
description: Sample schema
model:
  - name: field1
    description: This is field 1

  - name: field2";

        var schema = SchemaModel.FromYaml(yaml);

        Assert.Equal("Sample schema", schema.Description);
        Assert.Equal(2, schema.Model.Count);
        Assert.Equal("field1", schema.Model[0].Name);
        Assert.Equal("This is field 1", schema.Model[0].Description);
        Assert.Equal("field2", schema.Model[1].Name);
        Assert.Null(schema.Model[1].Description);
    }

    [Fact]
    public void FromYaml_MissingDescription_ThrowsException()
    {
        const string yaml = @"
name: Sample
fields:
  - name: field1
    description: This is field 1";

        Assert.Throws<YamlDotNet.Core.YamlException>(() => SchemaModel.FromYaml(yaml));
    }
}
