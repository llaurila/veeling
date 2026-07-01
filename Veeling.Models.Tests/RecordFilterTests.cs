namespace Veeling.Models.Tests;

public class RecordFilterTests
{
    private const string FormatErrorMessage = "Expected format: <schema>.<field>:<lang> (e.g. Login.UsernameLabel:fi or *.*:*).";

    [Fact]
    public void Constructor_Valid_SetsPatterns()
    {
        bool success = RecordFilter.TryParse("schema.field:en", out RecordFilter? spec, out _);

        Assert.True(success);
        Assert.NotNull(spec);

        Assert.Equal("schema", spec.Schema.ToString());
        Assert.Equal("field", spec.Field.ToString());
        Assert.Equal("schema.field:en", spec.ToString());
    }

    [Fact]
    public void Constructor_InvalidFormat_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => RecordFilter.Parse("schema"));
        Assert.Equal(FormatErrorMessage, ex.Message);
    }

    [Fact]
    public void Constructor_InvalidSchemaPattern_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => RecordFilter.Parse("schema!.field"));
        Assert.Equal(FormatErrorMessage, ex.Message);
    }

    [Fact]
    public void Constructor_InvalidFieldPattern_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => RecordFilter.Parse("schema.field!"));
        Assert.Equal(FormatErrorMessage, ex.Message);
    }

    [Theory]
    [InlineData("schema.field:en", "schema", "field", "en", true)]
    [InlineData("schema.*:en", "schema", "field", "en", true)]
    [InlineData("sch?ma.field:en", "schema", "field", "en", true)]
    [InlineData("schema.f?eld:en", "schema", "field", "en", true)]
    [InlineData("*.*:*", "foo", "bar", "en", true)]
    [InlineData("schema.field:en", "other", "field", "en", false)]
    [InlineData("schema.field:en", "schema", "other", "en", false)]
    public void IsMatch_EvaluatesPatterns(string pattern, string schemaName, string fieldName, string language, bool expected)
    {
        RecordFilter spec = RecordFilter.Parse(pattern);
        Assert.Equal(expected, spec.Matches(schemaName, fieldName, language));
    }

    [Theory]
    [InlineData("schema.field:en", true)]
    [InlineData("*.field:en", false)]
    [InlineData("schema.*:en", false)]
    [InlineData("schema.field:*", false)]
    [InlineData("sche?a.field:en", false)]
    public void IsAbsolute_EvaluatesPatterns(string pattern, bool expected)
    {
        RecordFilter spec = RecordFilter.Parse(pattern);
        Assert.Equal(expected, spec.IsAbsolute);
    }
}
