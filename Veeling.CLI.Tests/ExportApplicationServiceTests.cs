using Veeling.CLI.Providers;
using Veeling.Core.Application;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public sealed class ExportApplicationServiceTests
{
    [Fact]
    public void Execute_WhenSelectorOmitted_DefaultsToAllRecordsSelector()
    {
        Project project = MockData.GetMockProject("foobar");
        RecordFilter? observed = null;

        MockProjectDataSession session = new(project)
        {
            OnGet = recordSpec =>
            {
                observed = recordSpec;
                return [];
            }
        };

        ExportApplicationService service = new(new StubSessionFactory(session));

        _ = service.Execute(new ExportCommandRequest(
            Project: project,
            Selector: null,
            Format: ExportOutputFormat.Json
        ));

        Assert.NotNull(observed);
        Assert.Equal("*.*:*", observed!.Original);
    }

    [Theory]
    [InlineData("Schema1.Field1:en")]
    [InlineData("MySchema.*:*")]
    [InlineData("*.*:en")]
    public void Execute_WhenSelectorProvided_ForwardsRecordFilterSemantics(string selector)
    {
        Project project = MockData.GetMockProject("foobar");
        RecordFilter? observed = null;

        MockProjectDataSession session = new(project)
        {
            OnGet = recordSpec =>
            {
                observed = recordSpec;
                return [];
            }
        };

        ExportApplicationService service = new(new StubSessionFactory(session));

        _ = service.Execute(new ExportCommandRequest(
            Project: project,
            Selector: selector,
            Format: ExportOutputFormat.Yaml
        ));

        Assert.NotNull(observed);
        Assert.Equal(selector, observed!.Original);
    }

    [Fact]
    public void Execute_WhenSelectorInvalid_ThrowsValidationError()
    {
        Project project = MockData.GetMockProject("foobar");
        MockProjectDataSession session = new(project)
        {
            OnGet = _ => []
        };

        ExportApplicationService service = new(new StubSessionFactory(session));

        ArgumentException ex = Assert.Throws<ArgumentException>(() => service.Execute(new ExportCommandRequest(
            Project: project,
            Selector: "invalid",
            Format: ExportOutputFormat.Json
        )));

        Assert.Contains("Expected format: <schema>.<field>:<lang>", ex.Message, StringComparison.Ordinal);
    }

    private sealed class StubSessionFactory(IProjectDataSession session) : IProjectDataSessionFactory
    {
        public IProjectDataSession Open(Project project)
        {
            return session;
        }
    }
}
