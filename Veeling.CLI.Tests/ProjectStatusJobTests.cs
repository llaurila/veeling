using Veeling.Models;

namespace Veeling.CLI.Tests;

public class ProjectStatusJobTests
{
    private readonly MockProjectDataSession session;
    private readonly ProjectStatusJob job;

    public ProjectStatusJobTests()
    {
        Project project = MockData.GetMockProject("foobar");
        session = new MockProjectDataSession(project);
        job = new(session);
    }

    [Fact]
    public void NoProject_ThrowsInvalidOperation()
    {
        ProjectStatusJob job = new(new MockProjectDataSession());
        Assert.Throws<InvalidOperationException>(() => job.Run());
    }

    [Fact]
    public void EmptyProject_NoIssues()
    {
        session.OnGet = _ => [];
        int code = job.Run();
        Assert.Equal(ProjectStatusJob.Exit_NoIssues, code);
    }

    [Fact]
    public void OneGoodMasterRecord_NoIssues()
    {
        session.OnGet = _ => [
            new DataRetrieveResult
            (
                new DataModel { Name = "Field1", Value = "Hello" },
                new RecordLocator("Schema1", "Field1", "fr")
            )
        ];
        int code = job.Run();
        Assert.Equal(ProjectStatusJob.Exit_NoIssues, code);
    }

    [Fact]
    public void NoMasterRecord_MissingMaster()
    {
        session.OnGet = recordFilter =>
        {
            if (recordFilter.Language.IsAny)
            {
                return [GetDataRetrieveResult("en"), GetDataRetrieveResult("fr")];
            }

            return [new DataRetrieveResult
            (
                DataModel: null,
                new RecordLocator("Schema1", "Field1", recordFilter.Language.ToString())
            )];
        };

        job.OnIssue += (status, drr) =>
        {
            Assert.Equal(DataRetrieveResultStatus.MissingMaster, status);
            Assert.Equal("Schema1", drr.RecordLocator.Schema);
            Assert.Equal("Field1", drr.RecordLocator.Field);
            Assert.Equal("en", drr.RecordLocator.Language.ToString());
        };

        int code = job.Run();
        Assert.Equal(ProjectStatusJob.Exit_FatalError, code);
    }

    private static DataRetrieveResult GetDataRetrieveResult(string language)
    {
        return new DataRetrieveResult
        (
            new DataModel
            {
                Name = "Field1",
                Value = "Hello",
                Meta = GetValidMeta()
            },
            new RecordLocator("Schema1", "Field1", language)
        );
    }

    private static DataMetaModel GetValidMeta()
    {
        return new DataMetaModel
        {
            Status = DataStatus.Approved
        };
    }
}
