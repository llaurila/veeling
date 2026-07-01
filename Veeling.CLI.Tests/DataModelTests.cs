using Veeling.Models;

namespace Veeling.CLI.Tests;

public class DataModelTests
{
    [Fact]
    public void SourceHash_RoundTripsWhenLineEndingsNormalize()
    {
        var model = new DataModel
        {
            Name = "greeting",
            Value = "Hello\r\nWorld",
            Meta = new DataMetaModel()
        };

        var language = new Language("en");
        model.Meta.UpdateSourceHash(language, model.Name, model.Value);

        var roundtrippedValue = model.Value.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.False(model.Meta.IsSourceChanged(language, model.Name, roundtrippedValue));
    }
}
