using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI;

public enum DataRetrieveResultStatus
{
    Missing,
    MissingMaster,
    SourceChange,
    NeedsApproval
}

public delegate void ReportDataRetrieveResultStatus(DataRetrieveResultStatus status, DataRetrieveResult drr);

public class ProjectStatusJob(IProjectDataSession session)
{
    public const int Exit_NoIssues = 0;
    public const int Exit_WithIssues = 1;
    public const int Exit_FatalError = 2;

    public event ReportDataRetrieveResultStatus? OnIssue;

    private bool hasIssues;

    protected void Issue(DataRetrieveResultStatus status, DataRetrieveResult drr)
    {
        hasIssues = true;
        OnIssue?.Invoke(status, drr);
    }

    public int Run()
    {
        ProjectModel project = session.Project.Model;
        Language masterLang = project.MasterLanguage;

        List<DataRetrieveResult> data = [.. session.Get("*.*:*")];
        data.RemoveAll(drr => drr.RecordLocator.Language.Equals(masterLang));

        hasIssues = false;

        foreach (DataRetrieveResult drr in data)
        {
            RecordLocator masterRecordLocator = drr.RecordLocator.InLanguage(masterLang);

            DataModel? master = session.Get(masterRecordLocator)
                .Select(result => result.DataModel)
                .SingleOrDefault();

            if (master is null)
            {
                Issue(DataRetrieveResultStatus.MissingMaster, drr);
                return Exit_FatalError;
            }

            if (drr.DataModel is null)
            {
                Issue(DataRetrieveResultStatus.Missing, drr);
                continue;
            }

            DataModel record = drr.DataModel;

            if (record.Meta?.IsSourceChanged(masterLang, master.Name, master.Value) ?? false)
            {
                Issue(DataRetrieveResultStatus.SourceChange, drr);
            }
            else if (record.Meta?.NeedsApproval() ?? true)
            {
                Issue(DataRetrieveResultStatus.NeedsApproval, drr);
            }
        }

        return hasIssues ? Exit_WithIssues : Exit_NoIssues;
    }
}
