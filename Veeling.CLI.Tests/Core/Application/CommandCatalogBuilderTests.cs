using Microsoft.Extensions.DependencyInjection;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class CommandCatalogBuilderTests
{
    [Fact]
    public void Build_IncludesLiveCommandSurfaceAndCuratedHints()
    {
        using ServiceProvider serviceProvider = CliTestHost.CreateServiceProvider();
        ICommandCatalogBuilder builder = serviceProvider.GetRequiredService<ICommandCatalogBuilder>();

        CommandCatalog catalog = builder.Build();

        string[] topLevel = [.. catalog.Commands
            .Where(command => command.PathSegments.Count == 1)
            .Select(command => command.PathSegments[0])
            .OrderBy(static name => name, StringComparer.Ordinal)];

        Assert.Equal(
            ["ai", "config", "export", "init", "modify", "onboard", "publish", "status", "translate", "update"],
            topLevel);

        CommandCatalogEntry ai = Assert.Single(catalog.Commands, command => command.PathSegments.SequenceEqual(["ai"]));
        Assert.Contains("ask", ai.Aliases);
        Assert.NotNull(ai.CuratedHint);
        Assert.Contains("Never resolve to ai/ask recursively", ai.CuratedHint!, StringComparison.OrdinalIgnoreCase);

        CommandCatalogEntry modify = Assert.Single(catalog.Commands, command => command.PathSegments.SequenceEqual(["modify"]));
        Assert.Contains(modify.Options, option => string.Equals(option.Name, "--force", StringComparison.Ordinal));
        Assert.Contains(modify.Arguments, argument => string.Equals(argument.Name, "record-spec", StringComparison.Ordinal));

        CommandCatalogEntry translate = Assert.Single(catalog.Commands, command => command.PathSegments.SequenceEqual(["translate"]));
        Assert.Contains(translate.Options, option => string.Equals(option.Name, "--changed", StringComparison.Ordinal));
    }
}
