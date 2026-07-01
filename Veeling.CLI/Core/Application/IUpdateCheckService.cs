namespace Veeling.Core.Application;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
