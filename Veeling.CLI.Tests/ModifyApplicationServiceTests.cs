using Veeling.CLI.Exceptions;
using Veeling.CLI.Providers;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class ModifyApplicationServiceTests
{
    [Fact]
    public void Execute_WhenSaveFails_ThrowsPersistenceExceptionWithInnerError()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            SaveChangesException = new InvalidOperationException("disk full"),
            OnGet = _ =>
            [
                new DataRetrieveResult(
                    new DataModel
                    {
                        Name = "Field1",
                        Value = "Hello",
                        Meta = new DataMetaModel
                        {
                            Status = DataStatus.NeedsReview
                        }
                    },
                    new RecordLocator("Schema1", "Field1", "en")
                )
            ]
        };

        ModifyApplicationService service = new(new StubSessionFactory(session));

        ModifyCommandRequest request = new(
            Project: project,
            RecordSpec: RecordFilter.Parse("Schema1.Field1:en"),
            By: "tester",
            Value: "Updated",
            Status: null,
            Comment: null,
            Force: false
        );

        PersistenceException ex = Assert.Throws<PersistenceException>(() => service.Execute(request));

        Assert.Contains("Failed to persist modify command changes", ex.Message, StringComparison.Ordinal);
        Assert.Equal("disk full", ex.InnerException?.Message);
    }

    private sealed class StubSessionFactory(IProjectDataSession session) : IProjectDataSessionFactory
    {
        public IProjectDataSession Open(Project project)
        {
            return session;
        }
    }
}
