using Veeling.CLI;
using Veeling.CLI.Exceptions;
using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.Core.Application;

public sealed record ModifyCommandRequest(
    Project Project,
    RecordFilter RecordSpec,
    string By,
    string? Value,
    DataStatus? Status,
    string? Comment,
    bool Force
);

public sealed record ModifyCommandResult(bool HasChanges);

public sealed class ModifyApplicationService(IProjectDataSessionFactory sessionFactory)
{
    public ModifyCommandResult Execute(ModifyCommandRequest request)
    {
        if (!request.RecordSpec.IsAbsolute && request.Value is not null && !request.Force)
        {
            throw new ArgumentException(string.Join(
                Environment.NewLine,
                "You requested to set multiple records to the same value.",
                "If you really want to do this, use --force."
            ));
        }

        IProjectDataSession session = sessionFactory.Open(request.Project);
        DataRetrieveResult[] results = [.. session.Get(request.RecordSpec)];

        foreach (DataRetrieveResult result in results)
        {
            RecordModifierService modifier = new(session, result);
            modifier.Modify(request.By, request.Value, request.Status, request.Comment);
        }

        if (session.HasPendingChanges)
        {
            try
            {
                session.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new PersistenceException(
                    "Failed to persist modify command changes.",
                    ex
                );
            }

            return new ModifyCommandResult(true);
        }

        return new ModifyCommandResult(false);
    }
}
