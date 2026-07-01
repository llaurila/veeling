namespace Veeling.CLI.Tests;

public static class MockData
{
    public static Project GetMockProject(string projectName)
    {
        return new Project(GetMockProjectFile(projectName));
    }

    public static FileInfo GetMockProjectFile(string projectName)
    {
        return new FileInfo(
            Path.Combine(
                GetMockProjectDirectory(projectName).FullName,
                "Project.yaml"
            )
        );
    }

    public static DirectoryInfo GetMockProjectDirectory(string projectName)
    {
        return new DirectoryInfo(
            Path.Combine(GetMockDataDirectory().FullName, projectName)
        );
    }

    public static DirectoryInfo GetMockDataDirectory()
    {
        return new DirectoryInfo(
            Path.Combine(GetVSProjectRoot().FullName, "Data")
        );
    }

    public static DirectoryInfo GetVSProjectRoot()
    {
        DirectoryInfo di = new(AppContext.BaseDirectory);
        while (di is not null && di.GetFiles("*.csproj").Length == 0)
        {
            di = di.Parent ?? throw new InvalidOperationException("Could not find project root directory.");
        }

        return di!;
    }
}
