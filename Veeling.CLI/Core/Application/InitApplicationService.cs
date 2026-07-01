using Veeling.Models;
using Veeling.CLI;

namespace Veeling.Core.Application;

public sealed record InitProjectRequest(DirectoryInfo ProjectRoot, ProjectModel Model);

public sealed record InitProjectResult(
    bool Success,
    IReadOnlyList<string> OutputLines,
    IReadOnlyList<string> ErrorLines
);

public sealed class InitApplicationService
{
    public InitProjectResult Execute(InitProjectRequest request)
    {
        ProjectInitializer initializer = new();

        List<string> output = [];
        List<string> errors = [];

        initializer.OnLog += output.Add;
        initializer.OnLogError += errors.Add;

        bool success = initializer.Initialize(request.ProjectRoot, request.Model);
        return new InitProjectResult(success, output, errors);
    }
}
