using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Veeling.CLI.Configuration;

namespace Veeling.CLI.Providers;

public sealed class ConfiguredProjectDataSessionFactory(
    IServiceProvider serviceProvider,
    IOptions<ProjectDataProviderOptions> options) : IProjectDataSessionFactory
{
    public IProjectDataSession Open(Project project)
    {
        string providerType = options.Value.Type;

        try
        {
            return serviceProvider.GetRequiredKeyedService<IProjectDataSessionFactory>(providerType)
                .Open(project);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Project data provider '{providerType}' is not registered.",
                ex
            );
        }
    }
}
