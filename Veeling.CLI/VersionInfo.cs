using System.Reflection;

namespace Veeling.CLI;

internal static class VersionInfo
{
    public static string GetCurrentVersion()
    {
        string? informational = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+', 2)[0];
        }

        Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is not null)
        {
            return version.ToString(3);
        }

        return "0.0.0";
    }
}
