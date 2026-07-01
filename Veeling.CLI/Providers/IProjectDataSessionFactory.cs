namespace Veeling.CLI.Providers;

public interface IProjectDataSessionFactory
{
    IProjectDataSession Open(Project project);
}
