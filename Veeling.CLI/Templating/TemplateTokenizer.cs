namespace Veeling.CLI.Templating;

public static class TemplateTokenizer
{
    private const string Open = "${";
    private const string Close = "}";

    public static IEnumerable<TemplateToken> Tokenize(string template)
    {
        int pos = 0;

        while (pos < template.Length)
        {
            int start = template.IndexOf(Open, pos, StringComparison.Ordinal);
            if (start < 0)
            {
                yield return (new TemplateToken(template[pos..], isVariable: false));
                break;
            }

            if (start > pos)
                yield return (new TemplateToken(template[pos..start], isVariable: false));

            int end = template.IndexOf(Close, start + Open.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                yield return (new TemplateToken(template[start..], isVariable: false));
                break;
            }

            string var = template[(start + Open.Length)..end].Trim();
            yield return (new TemplateToken(var, isVariable: true));

            pos = end + Close.Length;
        }
    }
}
