using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class MockProjectDataSession : IProjectDataSession
{
    private Project? project;

    public MockProjectDataSession()
    {
    }

    public MockProjectDataSession(Project project)
    {
        this.project = project;
    }

    public Func<RecordFilter, IEnumerable<DataRetrieveResult>>? OnGet { get; set; }

    public Action<RecordLocator, DataModel>? OnSet { get; set; }

    public Project Project => project ?? throw new InvalidOperationException();

    public bool HasPendingChanges { get; set; }

    public int SaveChangesCalls { get; private set; }

    public int DiscardChangesCalls { get; private set; }

    public Exception? SaveChangesException { get; set; }

    public IEnumerable<DataRetrieveResult> Get(RecordFilter recordSpec)
    {
        return (OnGet ?? throw new InvalidOperationException("No getter defined."))(recordSpec);
    }

    public void SaveChanges()
    {
        if (SaveChangesException is not null)
        {
            throw SaveChangesException;
        }

        SaveChangesCalls++;
        HasPendingChanges = false;
    }

    public void Set(RecordLocator recordLocator, DataModel record)
    {
        HasPendingChanges = true;
        OnSet?.Invoke(recordLocator, record);
    }

    public void DiscardChanges()
    {
        DiscardChangesCalls++;
        HasPendingChanges = false;
    }

    public void SetProject(Project project)
    {
        this.project = project;
    }
}
