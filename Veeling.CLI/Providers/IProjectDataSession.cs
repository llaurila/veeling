using Veeling.Models;

namespace Veeling.CLI.Providers;

public interface IProjectDataSession
{
    Project Project { get; }

    IEnumerable<DataRetrieveResult> Get(RecordFilter recordSpec);

    void Set(RecordLocator recordLocator, DataModel record);

    bool HasPendingChanges { get; }

    void SaveChanges();

    void DiscardChanges();
}
