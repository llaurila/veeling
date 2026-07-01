using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Veeling.Models;

public static class YamlConfig
{
    public static readonly INamingConvention NamingConvention = UnderscoredNamingConvention.Instance;
}
