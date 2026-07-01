using Veeling.CLI.Exceptions;
using Veeling.Core.Application;

namespace Veeling.CLI.Tests.Core.Application;

public sealed class ConfigApplicationServiceTests
{
    [Fact]
    public void Execute_GlobalReadOutsideProject_ReturnsGlobalValue()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            FileInfo globalConfig = new(Path.Combine(sandbox.FullName, "global.veeling.yaml"));
            ConfigApplicationService service = new(new FixedGlobalConfigFileLocator(globalConfig));

            service.Execute(new ConfigCommandRequest(
                ProjectFile: new FileInfo(Path.Combine(sandbox.FullName, "Project.yaml")),
                ConfigDirectory: sandbox,
                Local: false,
                Global: true,
                Key: "username",
                Value: "global-user"
            ));

            ConfigCommandResult result = service.Execute(new ConfigCommandRequest(
                ProjectFile: new FileInfo(Path.Combine(sandbox.FullName, "Project.yaml")),
                ConfigDirectory: sandbox,
                Local: false,
                Global: true,
                Key: "username",
                Value: null
            ));

            Assert.Single(result.OutputLines);
            Assert.Equal("global-user", result.OutputLines[0]);
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    [Fact]
    public void Execute_UnscopedOutsideProject_ThrowsActionableError()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            ConfigApplicationService service = new(new FixedGlobalConfigFileLocator(new FileInfo(Path.Combine(sandbox.FullName, "global.veeling.yaml"))));

            CommandExecutionException ex = Assert.Throws<CommandExecutionException>(() => service.Execute(new ConfigCommandRequest(
                ProjectFile: new FileInfo(Path.Combine(sandbox.FullName, "Project.yaml")),
                ConfigDirectory: sandbox,
                Local: false,
                Global: false,
                Key: "username",
                Value: null
            )));

            Assert.Contains("No project found", ex.Message, StringComparison.Ordinal);
            Assert.Contains("--global", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    [Fact]
    public void Execute_LocalAndGlobalTogether_ThrowsActionableError()
    {
        DirectoryInfo sandbox = CreateSandbox();

        try
        {
            ConfigApplicationService service = new(new FixedGlobalConfigFileLocator(new FileInfo(Path.Combine(sandbox.FullName, "global.veeling.yaml"))));

            CommandExecutionException ex = Assert.Throws<CommandExecutionException>(() => service.Execute(new ConfigCommandRequest(
                ProjectFile: new FileInfo(Path.Combine(sandbox.FullName, "Project.yaml")),
                ConfigDirectory: sandbox,
                Local: true,
                Global: true,
                Key: "username",
                Value: "alice"
            )));

            Assert.Contains("--local", ex.Message, StringComparison.Ordinal);
            Assert.Contains("--global", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (sandbox.Exists)
            {
                sandbox.Delete(true);
            }
        }
    }

    [Fact]
    public void Execute_UnscopedInProject_UsesLocalOverGlobalPrecedence()
    {
        DirectoryInfo projectDirectory = CreateSandbox();

        try
        {
            FileInfo projectFile = TestProjectFactory.CreateProjectFile(projectDirectory);
            FileInfo globalConfig = new(Path.Combine(projectDirectory.FullName, "global.veeling.yaml"));
            ConfigApplicationService service = new(new FixedGlobalConfigFileLocator(globalConfig));

            service.Execute(new ConfigCommandRequest(projectFile, projectDirectory, Local: false, Global: true, Key: "username", Value: "global-user"));
            service.Execute(new ConfigCommandRequest(projectFile, projectDirectory, Local: true, Global: false, Key: "username", Value: "local-user"));

            ConfigCommandResult result = service.Execute(new ConfigCommandRequest(projectFile, projectDirectory, Local: false, Global: false, Key: "username", Value: null));

            Assert.Single(result.OutputLines);
            Assert.Equal("local-user", result.OutputLines[0]);
        }
        finally
        {
            if (projectDirectory.Exists)
            {
                projectDirectory.Delete(true);
            }
        }
    }

    private static DirectoryInfo CreateSandbox()
    {
        string path = Path.Combine(Path.GetTempPath(), "Veeling.ConfigAppServiceTests", Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path);
    }

    private sealed class FixedGlobalConfigFileLocator(FileInfo file) : IGlobalConfigFileLocator
    {
        public FileInfo GetGlobalConfigFile() => file;
    }
}
