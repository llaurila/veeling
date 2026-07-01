using Veeling.CLI;
using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.Core.Application;

public enum StatusOutputKind
{
    Issue,
    NoIssues
}

public sealed record StatusIssueOutput(
    StatusOutputKind Kind,
    DataRetrieveResultStatus? Status,
    DataRetrieveResult? Result
);

public sealed record StatusCommandResult(
    int ExitCode,
    string ProjectName,
    IReadOnlyList<StatusIssueOutput> Output
);

public sealed class StatusApplicationService(IProjectDataSessionFactory sessionFactory)
{
    public StatusCommandResult Execute(Project project)
    {
        IProjectDataSession session = sessionFactory.Open(project);
        ProjectStatusJob job = new(session);

        List<StatusIssueOutput> output = [];

        job.OnIssue += (status, drr) =>
        {
            output.Add(new StatusIssueOutput(StatusOutputKind.Issue, status, drr));
        };

        int code = job.Run();

        if (code == ProjectStatusJob.Exit_NoIssues)
        {
            output.Add(new StatusIssueOutput(StatusOutputKind.NoIssues, null, null));
        }

        return new StatusCommandResult(code, session.Project.Model.Name, output);
    }
}
