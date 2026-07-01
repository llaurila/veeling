namespace Veeling.Core.Application;

public interface IReleaseMetadataClient
{
    Task<ReleaseMetadata> FetchAsync(CancellationToken cancellationToken);
}
