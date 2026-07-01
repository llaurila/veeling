using Veeling.CLI.Providers;

namespace Veeling.CLI.Tests;

public class ProviderAuthFailureClassifierTests
{
    private readonly ProviderAuthFailureClassifier sut = new();

    [Fact]
    public void IsAuthenticationFailure_DirectUnauthorizedMessage_ReturnsTrue()
    {
        Exception ex = new InvalidOperationException("Unauthorized: invalid api key");

        bool result = sut.IsAuthenticationFailure(ex);

        Assert.True(result);
    }

    [Fact]
    public void IsAuthenticationFailure_NestedCredentialMessage_ReturnsTrue()
    {
        Exception ex = new InvalidOperationException(
            "Top-level wrapper",
            new Exception("Credential rejected by provider")
        );

        bool result = sut.IsAuthenticationFailure(ex);

        Assert.True(result);
    }

    [Fact]
    public void IsAuthenticationFailure_NonAuthMessage_ReturnsFalse()
    {
        Exception ex = new InvalidOperationException("Provider timeout while generating response");

        bool result = sut.IsAuthenticationFailure(ex);

        Assert.False(result);
    }
}
