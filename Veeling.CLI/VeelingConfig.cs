using Veeling.Models;
using Veeling.CLI.Exceptions;
using YamlDotNet.Serialization;

namespace Veeling.CLI;

public class VeelingConfig
{
    public const string ConfigFileName = ".veeling.yaml";

    public static readonly string[] Keys = [
        "username",
        "openai_model",
        "openai_apikey",
        "gemini_model",
        "gemini_apikey",
        "claude_model",
        "claude_apikey",
        "claude_max_tokens",
        "llm_provider",
        "intent_parser_provider",
        "intent_parser_model",
        "update_check_enabled"
    ];

    private readonly FileInfo globalConfigFile;

    private readonly FileInfo? localConfigFile;

    private readonly Lazy<Dictionary<string, string>> globalConfig;

    private readonly Lazy<Dictionary<string, string>> localConfig;

    public VeelingConfig(DirectoryInfo? projectDir = null, IGlobalConfigFileLocator? globalConfigFileLocator = null)
    {
        FileInfo resolvedGlobalConfigFile = (globalConfigFileLocator ?? new UserProfileGlobalConfigFileLocator()).GetGlobalConfigFile();
        globalConfigFile = new FileInfo(resolvedGlobalConfigFile.FullName);

        if (projectDir is not null)
        {
            localConfigFile = new FileInfo(Path.Combine(projectDir.FullName, ConfigFileName));
        }

        localConfig = new(() =>
        {
            if (localConfigFile is null || !localConfigFile.Exists) return [];
            return FromYaml(File.ReadAllText(localConfigFile.FullName));
        });

        globalConfig = new(() =>
        {
            if (!globalConfigFile.Exists) return [];
            return FromYaml(File.ReadAllText(globalConfigFile.FullName));
        });
    }

    public FileInfo GlobalConfigFile => globalConfigFile;

    public string? GetValue(string key)
    {
        ValidateKey(key);

        if (localConfig.Value.TryGetValue(key, out string? localValue))
        {
            return localValue;
        }
        if (globalConfig.Value.TryGetValue(key, out string? globalValue))
        {
            return globalValue;
        }

        return null;
    }

    public string? GetLocalValue(string key)
    {
        ValidateKey(key);
        return localConfig.Value.TryGetValue(key, out string? value) ? value : null;
    }

    public string? GetGlobalValue(string key)
    {
        ValidateKey(key);
        return globalConfig.Value.TryGetValue(key, out string? value) ? value : null;
    }

    public IEnumerable<KeyValuePair<string, string>> GetAllValues()
    {
        HashSet<string> seenKeys = [];
        foreach (var kvp in localConfig.Value)
        {
            seenKeys.Add(kvp.Key);
            yield return kvp;
        }
        foreach (var kvp in globalConfig.Value)
        {
            if (!seenKeys.Contains(kvp.Key))
            {
                yield return kvp;
            }
        }
    }

    public IEnumerable<KeyValuePair<string, string>> GetAllLocalValues()
    {
        foreach (var kvp in localConfig.Value)
        {
            yield return kvp;
        }
    }

    public IEnumerable<KeyValuePair<string, string>> GetAllGlobalValues()
    {
        foreach (var kvp in globalConfig.Value)
        {
            yield return kvp;
        }
    }

    public void SetLocalValue(string key, string? value)
    {
        ValidateKey(key);

        if (localConfigFile is null)
        {
            throw new InvalidOperationException(
                "Cannot set local config value because no local config file was specified."
            );
        }

        if (value is null)
        {
            localConfig.Value.Remove(key);
        }
        else
        {
            localConfig.Value[key] = value;
        }
        SaveConfig(localConfigFile, localConfig.Value);
    }

    public void SetGlobalValue(string key, string? value)
    {
        ValidateKey(key);

        if (value is null)
        {
            globalConfig.Value.Remove(key);
        }
        else
        {
            globalConfig.Value[key] = value;
        }
        SaveConfig(globalConfigFile, globalConfig.Value);
    }

    private static void ValidateKey(string key)
    {
        if (!Keys.Contains(key))
        {
            throw new ArgumentException($"Invalid config key: {key}", nameof(key));
        }
    }

    private static void SaveConfig(FileInfo fi, Dictionary<string, string> config)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();
        string yaml = serializer.Serialize(config);

        try
        {
            AtomicFile.WriteAllText(fi, yaml);
        }
        catch (Exception ex)
        {
            throw new PersistenceException(
                $"Failed to save config file '{fi.FullName}' atomically.",
                ex
            );
        }
    }

    private static Dictionary<string, string> FromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlConfig.NamingConvention)
            .Build();
        return deserializer.Deserialize<Dictionary<string, string>>(yaml);
    }
}
