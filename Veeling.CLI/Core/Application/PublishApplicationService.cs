using Veeling.CLI;

namespace Veeling.Core.Application;

public sealed class PublishApplicationService(ProjectPublisher projectPublisher)
{
    public string Execute(Project project)
    {
        return projectPublisher.Publish(project);
    }
}
