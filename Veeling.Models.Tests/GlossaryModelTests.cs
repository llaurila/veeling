namespace Veeling.Models.Tests;

public class GlossaryModelTests
{
    [Fact]
    public void FromYaml_ParsesValidGlossary()
    {
        const string yaml = @"
language: fi

entries:
  - term: Sign in
    translation: Kirjaudu sisaan
    status: approved
    note: Login button text
    forbidden_variants:
      - Kirjaantuminen
    applies_to:
      - ui
      - ai
";

        GlossaryModel glossary = GlossaryModel.FromYaml(yaml);

        Assert.Equal("fi", glossary.Language.Code);
        Assert.Single(glossary.Entries);

        GlossaryEntryModel entry = glossary.Entries[0];

        Assert.Equal("Sign in", entry.Term);
        Assert.Equal("Kirjaudu sisaan", entry.Translation);
        Assert.Equal(GlossaryEntryStatus.Approved, entry.Status);
        Assert.Equal("Login button text", entry.Note);
        Assert.Equal(["Kirjaantuminen"], entry.ForbiddenVariants);
        Assert.Equal([GlossaryAppliesTo.Ui, GlossaryAppliesTo.Ai], entry.AppliesTo);
    }

    [Fact]
    public void FromYaml_MissingAppliesTo_DefaultsToAllContexts()
    {
        const string yaml = @"
language: fi

entries:
  - term: Account
    translation: Tili
    status: preferred
";

        GlossaryModel glossary = GlossaryModel.FromYaml(yaml);

        GlossaryEntryModel entry = Assert.Single(glossary.Entries);

        Assert.Equal(
            [GlossaryAppliesTo.Ui, GlossaryAppliesTo.System, GlossaryAppliesTo.Ai],
            entry.AppliesTo
        );
    }
}
