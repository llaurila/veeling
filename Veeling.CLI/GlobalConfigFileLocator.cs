namespace Veeling.CLI;

public interface IGlobalConfigFileLocator
{
    FileInfo GetGlobalConfigFile();
}

public sealed class UserProfileGlobalConfigFileLocator : IGlobalConfigFileLocator
{
    public FileInfo GetGlobalConfigFile()
    {
        return new FileInfo(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                VeelingConfig.ConfigFileName
            )
        );
    }
}
