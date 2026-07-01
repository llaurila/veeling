using Veeling.Models;

namespace Veeling.CLI.Tests;

public class RecordModifierServiceTests
{
    private readonly MockProjectDataSession session;

    public RecordModifierServiceTests()
    {
        Project project = MockData.GetMockProject("foobar");
        session = new MockProjectDataSession(project);
    }

    [Fact]
    public void Edit_MasterRecord()
    {
        DataRetrieveResult drr = MakeDRR("Schema1.Field1:fr", "foo");
        RecordModifierService rms = new(session, drr);

        bool onSetCalled = false;

        session.OnSet += (rl, record) =>
        {
            onSetCalled = true;
            Assert.Equal("Schema1.Field1:fr", rl.ToString());
            Assert.Equal("bar", record.Value);
            Assert.Equal(DataStatus.Approved, record.Meta?.Status);
            Assert.Equal("Looks good to me.", record.Meta?.Comment);
        };

        DataModel result = rms.Modify("editor", "bar", DataStatus.Approved, "Looks good to me.");

        Assert.NotNull(result);
        Assert.True(onSetCalled);
    }

    [Fact]
    public void Edit_TranslationRecord_WhenSourceChanged_UpdatesSourceHash()
    {
        RecordLocator masterLocator = RecordLocator.Parse("Schema1.Field1:fr");
        RecordLocator translationLocator = RecordLocator.Parse("Schema1.Field1:en");

        DataMetaModel translationMeta = new() { Status = DataStatus.Approved };
        translationMeta.UpdateSourceHash(masterLocator, "Old source");

        DataRetrieveResult drr = MakeDRR(translationLocator.ToString(), "Bonjour", translationMeta);
        RecordModifierService rms = new(session, drr);

        DataModel masterRecord = new()
        {
            Name = masterLocator.Field,
            Value = "New source"
        };

        session.OnGet = recordFilter =>
        {
            Assert.Equal(masterLocator.ToString(), recordFilter.ToString());
            return [new DataRetrieveResult(masterRecord, masterLocator)];
        };

        bool onSetCalled = false;

        session.OnSet += (rl, record) =>
        {
            onSetCalled = true;
            Assert.Equal(translationLocator.ToString(), rl.ToString());
            Assert.Equal("Bonjour", record.Value);
            Assert.Equal(DataStatus.Approved, record.Meta?.Status);

            DataMetaModel expectedMeta = new();
            expectedMeta.UpdateSourceHash(masterLocator, masterRecord.Value);

            Assert.Equal(expectedMeta.SourceHash, record.Meta?.SourceHash);
            Assert.False(record.Meta?.IsSourceChanged(masterLocator, masterRecord.Value) ?? true);
        };

        DataModel result = rms.Modify("editor", null, DataStatus.Approved, null);

        Assert.NotNull(result);
        Assert.True(onSetCalled);
    }

    private static DataRetrieveResult MakeDRR(string recordLocator, string value, DataMetaModel? meta = null)
    {
        RecordLocator rl = RecordLocator.Parse(recordLocator);

        DataModel dm = new()
        {
            Name = rl.Field,
            Value = value,
            Meta = meta
        };

        return new DataRetrieveResult(dm, rl);
    }
}
