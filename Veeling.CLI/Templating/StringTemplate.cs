using System.Reflection;
using System.Text;

namespace Veeling.CLI.Templating;

public class StringTemplate(string template)
{
    private readonly Lazy<TemplateToken[]> tokens = new(
        () => [.. TemplateTokenizer.Tokenize(template)],
        isThreadSafe: false
    );

    public string Template { get; private set; } = template ?? throw new ArgumentNullException(nameof(template));

    public string Format(IDictionary<string, string> additionalVariables)
    {
        return InjectVariables(additionalVariables);
    }

    public string FormatWith<T>(T obj, IDictionary<string, string>? additionalVariables = null)
    {
        if (obj == null) return Template;
        var variables = ResolveVariables(obj).ToDictionary();

        if (additionalVariables is not null && additionalVariables.Count > 0)
        {
            foreach (var kv in additionalVariables)
            {
                variables[kv.Key] = kv.Value ?? string.Empty;
            }
        }

        return InjectVariables(variables);
    }

    public string InjectVariables(IDictionary<string, string> variables)
    {
        if (variables == null || variables.Count == 0) return Template;

        var sb = new StringBuilder();

        foreach (var t in tokens.Value)
        {
            if (!t.IsVariable)
            {
                sb.Append(t.Value);
            }
            else if (variables.TryGetValue(t.Value, out string? value) && value is not null)
            {
                sb.Append(value);
            }
            else
            {
                throw new KeyNotFoundException($"Variable '{t.Value}' not found in provided variables.");
            }
        }

        return sb.ToString();
    }

    public static IDictionary<string, string> GetVariableDictionary<T>(T obj)
        => ResolveVariables(obj).ToDictionary();

    public static IEnumerable<KeyValuePair<string, string>> ResolveVariables<T>(T obj)
        => ResolveVariables(obj!, parentPath: null, new HashSet<object>());

    private static IEnumerable<KeyValuePair<string, string>> ResolveVariables(
        object current,
        string? parentPath,
        ISet<object> visited)
    {
        if (current == null || visited.Contains(current)) yield break;

        visited.Add(current);

        var props = current.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (PropertyInfo prop in props)
        {
            var attr = prop.GetCustomAttribute<TemplateVariableAttribute>();
            if (attr == null) continue;

            string name = attr.Name ?? prop.Name;
            string path = parentPath == null ? name : $"{parentPath}.{name}";
            object? value = prop.GetValue(current);

            if (value == null) continue;

            if (prop.PropertyType == typeof(string))
            {
                yield return new KeyValuePair<string, string>(path, (string)value);
            }
            else
            {
                foreach (var kv in ResolveVariables(value, path, visited))
                {
                    yield return kv;
                }
            }
        }
    }
}
