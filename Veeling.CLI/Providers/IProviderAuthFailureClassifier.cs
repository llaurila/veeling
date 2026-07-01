namespace Veeling.CLI.Providers;

public interface IProviderAuthFailureClassifier
{
    bool IsAuthenticationFailure(Exception ex);
}
