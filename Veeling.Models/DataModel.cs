using YamlDotNet.Serialization;

namespace Veeling.Models;

public class DataModel
{
    public required string Name { get; init; }

    public required string Value { get; set; }

    public DataMetaModel? Meta { get; set; }

    /// <summary>
    /// Handles a possible change in the data status (stored in the Meta property). Updates the value
    /// and returns true if the value was changed, false otherwise. If the source hash has changed and
    /// newStatus is Approved, the hash will be updated, even if the status was already Approved.
    /// </summary>
    /// <param name="newStatus">The new status.</param>
    /// <param name="source">Source DataModel in the master language (or null if this is the master language)</param>
    /// <returns>True if changes were made, false otherwise.</returns>
    public bool HandleStatusChange(DataStatus newStatus, DataRetrieveResult? source)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentException($"Invalid status: {newStatus}");
        }

        bool sourceChange = false;

        if (source is not null && source.DataModel is not null)
        {
            sourceChange = Meta?.IsSourceChanged(
                source.RecordLocator, source.DataModel.Value) ?? false;

        }
        bool statusChange = Meta?.Status != newStatus;

        Meta ??= new DataMetaModel();

        if (sourceChange && newStatus == DataStatus.Approved)
        {
            Meta.UpdateSourceHash(
                source!.RecordLocator,
                source!.DataModel!.Value
            );
        }

        Meta.Status = newStatus;

        return sourceChange || statusChange;
    }

    public string ToYaml()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();
        return serializer.Serialize(this);
    }

    public static string ToYaml(DataModel[] data)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();
        return serializer.Serialize(data);
    }

    public static DataModel[] FromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .WithEnforceRequiredMembers()
            .Build();

        return deserializer.Deserialize<DataModel[]>(yaml);
    }
}
