using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI;

public class RecordModifierService(IProjectDataSession session, DataRetrieveResult result)
{
    public DataModel Modify(string by, string? value, DataStatus? status, string? comment)
    {
        DataModel record = result.DataModel ?? new DataModel
        {
            Name = result.RecordLocator.Field,
            Value = string.Empty
        };

        DataModel? masterRecord = null;

        ProjectModel project = session.Project.Model;
        bool isMasterLanguage = project.MasterLanguage.Equals(result.RecordLocator.Language);

        if (!isMasterLanguage)
        {
            masterRecord = session.Get(
                result.RecordLocator.InLanguage(project.MasterLanguage)
            ).Single().DataModel;
        }

        bool valueChanged = false;

        if (value is not null)
        {
            record.Value = value;
            valueChanged = true;
        }
        else
        {
            value = record.Value;
        }

        bool metaChanged = false;

        record.Meta ??= new();

        if (comment is not null)
        {
            record.Meta.Comment = comment;
            metaChanged = true;
        }

        if (status is not null)
        {
            metaChanged |= record.HandleStatusChange(
                status.Value,
                masterRecord is not null
                ? new DataRetrieveResult(
                    DataModel: masterRecord,
                    RecordLocator: result.RecordLocator.InLanguage(project.MasterLanguage)
                )
                : null);
        }
        else if (valueChanged && record.Meta.Status != DataStatus.New)
        {
            record.Meta.Status = DataStatus.NeedsReview;
            metaChanged = true;
        }

        if (!valueChanged && !metaChanged)
        {
            return record;
        }

        record.Meta.Tick(by);
        record.Meta.UpdateHash(result.RecordLocator, value);

        session.Set(result.RecordLocator, record);

        return record;
    }
}
