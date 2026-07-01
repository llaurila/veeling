namespace Veeling.Core.Application;

public static class ReleaseMetadataClientRegistration
{
    public const string HttpClientName = "ReleaseMetadata";

    public const string HttpClientLoggerCategoryPrefix = "System.Net.Http.HttpClient." + HttpClientName;
}
