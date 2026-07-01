namespace Veeling.CLI.Providers;

public interface ILLMProviderFactory
{
    ILLMProvider Create(DirectoryInfo projectDirectory);
}
