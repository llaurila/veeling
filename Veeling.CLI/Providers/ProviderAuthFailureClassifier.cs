namespace Veeling.CLI.Providers;

public sealed class ProviderAuthFailureClassifier : IProviderAuthFailureClassifier
{
    private static readonly string[] AuthFailureKeywords =
    [
        "authentication",
        "unauthorized",
        "invalid api key",
        "api key",
        "apikey",
        "credential",
        "forbidden"
    ];

    public bool IsAuthenticationFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (string.IsNullOrWhiteSpace(current.Message))
            {
                continue;
            }

            string normalized = current.Message.ToLowerInvariant();

            if (AuthFailureKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
