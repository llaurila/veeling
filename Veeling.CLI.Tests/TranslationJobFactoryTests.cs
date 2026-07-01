using Veeling.CLI.Exceptions;
using Veeling.CLI.Providers;
using Veeling.Models;

namespace Veeling.CLI.Tests;

public class TranslationJobFactoryTests
{
    [Fact]
    public void Create_AuthLikeInitializationFailure_ThrowsProviderAuthenticationException()
    {
        Project project = MockData.GetMockProject("foobar");
        TranslationJobFactory factory = new(
            new StubSessionFactory(project),
            new ThrowingProviderFactory(new InvalidOperationException("Unauthorized: invalid api key")),
            new GlossaryLoader(),
            new ProviderAuthFailureClassifier());

        ProviderAuthenticationException ex = Assert.Throws<ProviderAuthenticationException>(
            () => factory.Create(project, "Schema1", new Language("en"), new Language("fi"))
        );

        Assert.Equal(4, ex.ExitCode);
        Assert.Contains("Failed to initialize translation provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_NonAuthInitializationFailure_ThrowsProviderExecutionException()
    {
        Project project = MockData.GetMockProject("foobar");
        TranslationJobFactory factory = new(
            new StubSessionFactory(project),
            new ThrowingProviderFactory(new InvalidOperationException("Provider timeout")),
            new GlossaryLoader(),
            new ProviderAuthFailureClassifier());

        ProviderExecutionException ex = Assert.Throws<ProviderExecutionException>(
            () => factory.Create(project, "Schema1", new Language("en"), new Language("fi"))
        );

        Assert.Equal(3, ex.ExitCode);
        Assert.Contains("Failed to initialize translation provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubSessionFactory(Project project) : IProjectDataSessionFactory
    {
        public IProjectDataSession Open(Project ignored)
        {
            return new MockProjectDataSession(project);
        }
    }

    private sealed class ThrowingProviderFactory(Exception exception) : ILLMProviderFactory
    {
        public ILLMProvider Create(DirectoryInfo projectDirectory)
        {
            throw exception;
        }
    }
}
