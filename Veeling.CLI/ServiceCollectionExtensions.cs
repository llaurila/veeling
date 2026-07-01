using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veeling.CLI.Commands;
using Veeling.CLI.Configuration;
using Veeling.CLI.Providers;
using Veeling.Core.Application;

namespace Veeling.CLI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVeelingCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProjectDataProviderOptions>()
            .Bind(configuration.GetSection("ProjectDataProvider"))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Type),
                "Project data provider type is required.")
            .ValidateOnStart();

        services.AddOptions<UpdateCheckOptions>()
            .Bind(configuration.GetSection("UpdateCheck"));

        services.AddHttpClient<IReleaseMetadataClient, ReleaseMetadataClient>(ReleaseMetadataClientRegistration.HttpClientName)
            .RemoveAllLoggers();

        services.AddSingleton<IUpdateCheckCache, FileSystemUpdateCheckCache>();
        services.AddSingleton<IUpdateCheckService, UpdateCheckApplicationService>();
        services.AddSingleton<UpdateAdvisoryApplicationService>();
        services.AddSingleton<UpdateCheckBootstrapService>();

        services.AddKeyedSingleton<IProjectDataSessionFactory, FileSystemProjectDataSessionFactory>("FileSystem");
        services.AddSingleton<IProjectDataSessionFactory, ConfiguredProjectDataSessionFactory>();

        services.AddSingleton<ILLMProviderFactory, ConfiguredLLMProviderFactory>();
        services.AddSingleton<IIntentParserProvider, ConfiguredIntentParserProvider>();
        services.AddSingleton<IProviderAuthFailureClassifier, ProviderAuthFailureClassifier>();
        services.AddSingleton<IGlobalConfigFileLocator, UserProfileGlobalConfigFileLocator>();

        services.AddSingleton<ProjectPublisher>();
        services.AddSingleton<GlossaryLoader>();
        services.AddSingleton<TranslationJobFactory>();
        services.AddSingleton<ConfigApplicationService>();
        services.AddSingleton<InitApplicationService>();
        services.AddSingleton<PublishApplicationService>();
        services.AddSingleton<StatusApplicationService>();
        services.AddSingleton<ExportApplicationService>();
        services.AddSingleton<ModifyApplicationService>();
        services.AddSingleton<TranslateApplicationService>();
        services.AddSingleton<OnboardingApplicationService>();
        services.AddSingleton<ICommandCatalogBuilder, LiveCommandCatalogBuilder>();
        services.AddSingleton<IntentResolutionApplicationService>();

        services.AddSingleton<ICliCommand, InitCommand>();
        services.AddSingleton<ICliCommand, ConfigCommand>();
        services.AddSingleton<ICliCommand, PublishCommand>();
        services.AddSingleton<ICliCommand, StatusCommand>();
        services.AddSingleton<ICliCommand, ExportCommand>();
        services.AddSingleton<ICliCommand, ModifyCommand>();
        services.AddSingleton<ICliCommand, TranslateCommand>();
        services.AddSingleton<ICliCommand, OnboardCommand>();
        services.AddSingleton<ICliCommand, UpdateCommand>();
        services.AddSingleton<ICliCommand, AiCommand>();

        services.AddSingleton<App>();

        return services;
    }
}
