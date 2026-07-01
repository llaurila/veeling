using Veeling.CLI.Providers;
using Veeling.CLI.Exceptions;
using Veeling.Models;

namespace Veeling.CLI;

public class TranslationJobFactory(
    IProjectDataSessionFactory sessionFactory,
    ILLMProviderFactory llmProviderFactory,
    GlossaryLoader glossaryLoader,
    IProviderAuthFailureClassifier authFailureClassifier)
{
    public TranslationJob Create(Project project, string schemaName, Language sourceLang, Language targetLang)
    {
        ILLMProvider llmProvider;

        try
        {
            llmProvider = llmProviderFactory.Create(project.Directory);
        }
        catch (CommandExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            string message =
                $"Failed to initialize translation provider for schema '{schemaName}' ({sourceLang.Code} -> {targetLang.Code}): {ex.Message}";

            if (authFailureClassifier.IsAuthenticationFailure(ex))
            {
                throw new ProviderAuthenticationException(message, ex);
            }

            throw new ProviderExecutionException(message, ex);
        }

        return new TranslationJob(
            sessionFactory.Open(project),
            llmProvider,
            project,
            schemaName,
            sourceLang,
            targetLang,
            authFailureClassifier,
            glossaryLoader.Load(project, targetLang)
        );
    }
}
