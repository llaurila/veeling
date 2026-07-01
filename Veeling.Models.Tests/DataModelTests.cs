namespace Veeling.Models.Tests;

public class DataModelTests
{
    [Fact]
    public void HandleStatusChange_NoChanges_ReturnsFalse()
    {
        DataModel dm = CreateDataModel(DataStatus.Approved);

        bool changed = dm.HandleStatusChange(DataStatus.Approved, null);

        Assert.False(changed);
        Assert.Equal(DataStatus.Approved, dm.Meta?.Status);
    }

    [Fact]
    public void HandleStatusChange_StatusChange_ReturnsTrueAndUpdatesStatus()
    {
        DataModel dm = CreateDataModel(DataStatus.New);

        bool changed = dm.HandleStatusChange(DataStatus.NeedsReview, null);

        Assert.True(changed);
        Assert.Equal(DataStatus.NeedsReview, dm.Meta?.Status);
    }

    [Fact]
    public void HandleStatusChange_MetaNull_CreatesMetaAndReturnsTrue()
    {
        DataModel dm = new()
        {
            Name = "Field1",
            Value = "foo",
            Meta = null
        };

        bool changed = dm.HandleStatusChange(DataStatus.Approved, null);

        Assert.True(changed);
        Assert.NotNull(dm.Meta);
        Assert.Equal(DataStatus.Approved, dm.Meta?.Status);
    }

    [Fact]
    public void HandleStatusChange_SourceChangedAndApproved_UpdatesSourceHashAndReturnsTrue()
    {
        RecordLocator locator = new("schema", "field", "en");
        DataModel sourceModel = CreateDataModel(DataStatus.Unknown, "source");
        DataRetrieveResult source = new(sourceModel, locator);

        DataMetaModel meta = new()
        {
            Status = DataStatus.Approved
        };
        meta.UpdateSourceHash(locator, "old");

        DataModel dm = CreateDataModel(DataStatus.Approved, "target");
        dm.Meta = meta;

        DataMetaModel expectedMeta = new();
        expectedMeta.UpdateSourceHash(locator, sourceModel.Value);

        bool changed = dm.HandleStatusChange(DataStatus.Approved, source);

        Assert.True(changed);
        Assert.Equal(expectedMeta.SourceHash, dm.Meta?.SourceHash);
        Assert.Equal(DataStatus.Approved, dm.Meta?.Status);
    }

    [Fact]
    public void HandleStatusChange_InvalidEnum()
    {
        DataModel dm = CreateDataModel(DataStatus.Approved);
        Assert.Throws<ArgumentException>(() => dm.HandleStatusChange((DataStatus)999, null));
    }

    private static DataModel CreateDataModel(DataStatus status, string value = "foo")
    {
        return new DataModel
        {
            Name = "Field1",
            Value = value,
            Meta = new DataMetaModel
            {
                Status = status
            }
        };
    }
}
