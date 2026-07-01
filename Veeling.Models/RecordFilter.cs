using Veeling.Models;

namespace Veeling.Models;

public readonly record struct RecordKey(string Schema, string Field, Language Language);

public sealed class RecordFilter
{
    public string Original { get; }

    public GlobPattern Schema { get; }

    public GlobPattern Field { get; }

    public LangSpec Language { get; }

    private RecordFilter(string original, GlobPattern schema, GlobPattern field, LangSpec language)
    {
        Original = original;
        Schema = schema;
        Field = field;
        Language = language;
    }

    public bool IsAbsolute =>
        !Schema.HasWildcards &&
        !Field.HasWildcards &&
        !Language.IsAny;

    public bool Matches(string schema, string field, Language language)
        => Schema.IsMatch(schema) && Field.IsMatch(field) && Language.Matches(language.Code);

    public bool Matches(in RecordKey key)
        => Schema.IsMatch(key.Schema) && Field.IsMatch(key.Field) && Language.Matches(key.Language.Code);

    public static RecordFilter Parse(string spec)
    {
        if (!TryParse(spec, out var parsed, out var error))
            throw new ArgumentException(error);
        return parsed!;
    }

    public static bool TryParse(string spec, out RecordFilter? result, out string error)
    {
        result = null;
        error = "";

        if (string.IsNullOrWhiteSpace(spec))
        {
            error = "Record spec is empty.";
            return false;
        }

        spec = spec.Trim();

        // <schema-spec> "." <field-spec> ":" <lang-spec>
        int dot = spec.IndexOf('.');
        int colon = spec.LastIndexOf(':'); // safe: lang cannot contain ':'

        if (dot <= 0 || colon <= dot + 1 || colon == spec.Length - 1)
        {
            error = "Expected format: <schema>.<field>:<lang> (e.g. Login.UsernameLabel:fi or *.*:*).";
            return false;
        }

        string schemaSpec = spec[..dot];
        string fieldSpec = spec[(dot + 1)..colon];
        string langSpec = spec[(colon + 1)..];

        if (!IdentifierSpecValidator.IsValid(schemaSpec))
        {
            error = $"Invalid schema spec '{schemaSpec}'. Allowed: letters/digits/_ plus '*' and '?', and it must not start with a digit.";
            return false;
        }

        if (!IdentifierSpecValidator.IsValid(fieldSpec))
        {
            error = $"Invalid field spec '{fieldSpec}'. Allowed: letters/digits/_ plus '*' and '?', and it must not start with a digit.";
            return false;
        }

        if (!LangSpec.TryParse(langSpec, out var lang, out error))
            return false;

        var schema = GlobPattern.Compile(schemaSpec);
        var field = GlobPattern.Compile(fieldSpec);

        result = new RecordFilter(spec, schema, field, lang);
        return true;
    }

    public RecordFilter InLanguage(Language language)
    {
        if (!LangSpec.TryParse(language.Code, out LangSpec lang, out string error))
        {
            throw new InvalidOperationException($"Invalid language code in record spec '{Original}': {error}");
        }
        return new(Original, Schema, Field, lang);
    }

    public override string ToString() => Original;

    public static implicit operator RecordFilter(string s) => Parse(s);

    public static implicit operator RecordFilter(RecordLocator rl)
    {
        return Parse($"{rl.Schema}.{rl.Field}:{rl.Language.Code}");
    }
}

internal static class IdentifierSpecValidator
{
    // Your BNF excludes '_' as first char; I include it for consistency with identifiers.
    public static bool IsValid(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;

        // first char: letter, '_' or '*' or '?'
        char c0 = s[0];
        if (!(IsLetter(c0) || c0 == '_' || c0 == '*' || c0 == '?')) return false;

        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (!(IsLetter(c) || IsDigit(c) || c == '_' || c == '*' || c == '?')) return false;
        }

        return true;

        static bool IsLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        static bool IsDigit(char c) => (c >= '0' && c <= '9');
    }
}

public readonly struct LangSpec
{
    private readonly string? _code; // null means "*"

    private LangSpec(string? code) => _code = code;

    public bool IsAny => _code is null;

    public bool Matches(string language)
    {
        if (IsAny) return true;
        if (language is null) return false;
        return string.Equals(_code, language, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParse(string s, out LangSpec spec, out string error)
    {
        spec = default;
        error = "";

        if (string.IsNullOrWhiteSpace(s))
        {
            error = "Language spec is empty.";
            return false;
        }

        s = s.Trim();

        if (s == "*")
        {
            spec = new LangSpec(null);
            return true;
        }

        if (s.Length == 2 && IsLowerAsciiLetter(s[0]) && IsLowerAsciiLetter(s[1]))
        {
            spec = new LangSpec(s);
            return true;
        }

        error = $"Invalid language spec '{s}'. Expected '*' or a two-letter lower-case code like 'fi'.";
        return false;

        static bool IsLowerAsciiLetter(char c) => c >= 'a' && c <= 'z';
    }

    public override string ToString() => _code ?? "*";
}

public sealed class GlobPattern
{
    private readonly string _pattern;
    private readonly bool _hasWildcards;
    private readonly string[] _chunks;     // split by '*'
    private readonly bool _startsWithStar;
    private readonly bool _endsWithStar;
    private readonly int _minLength;       // pattern length minus '*' chars

    private GlobPattern(string pattern, bool hasWildcards, string[] chunks, bool startsWithStar, bool endsWithStar, int minLength)
    {
        _pattern = pattern;
        _hasWildcards = hasWildcards;
        _chunks = chunks;
        _startsWithStar = startsWithStar;
        _endsWithStar = endsWithStar;
        _minLength = minLength;
    }

    public static GlobPattern Compile(string pattern)
    {
        pattern ??= "";

        bool hasWildcards = pattern.IndexOfAny(new[] { '*', '?' }) >= 0;

        if (!hasWildcards)
        {
            return new GlobPattern(pattern, false, Array.Empty<string>(), false, false, pattern.Length);
        }

        bool startsWithStar = pattern.Length > 0 && pattern[0] == '*';
        bool endsWithStar = pattern.Length > 0 && pattern[^1] == '*';

        // Split by '*' (keep empty chunks; they’re harmless)
        string[] chunks = pattern.Split('*');

        int minLength = 0;
        for (int i = 0; i < pattern.Length; i++)
            if (pattern[i] != '*')
                minLength++;

        return new GlobPattern(pattern, true, chunks, startsWithStar, endsWithStar, minLength);
    }

    public bool HasWildcards => _hasWildcards;

    public bool IsMatch(string input)
    {
        input ??= "";

        if (!_hasWildcards)
            return string.Equals(_pattern, input, StringComparison.Ordinal);

        if (_pattern == "*")
            return true;

        if (input.Length < _minLength)
            return false;

        ReadOnlySpan<char> s = input.AsSpan();

        int chunkCount = _chunks.Length;

        // Edge case: pattern is something like "**" -> chunks are ["", "", ""] etc.
        // That should match anything (minLength already 0).
        bool anyRealChunk = false;
        for (int i = 0; i < chunkCount; i++)
        {
            if (_chunks[i].Length != 0) { anyRealChunk = true; break; }
        }
        if (!anyRealChunk)
            return true;

        int pos = 0;

        // Prefix anchored
        int idx = 0;
        if (!_startsWithStar)
        {
            string first = _chunks[0];
            if (first.Length == 0) return true; // defensive
            if (!MatchAt(s, 0, first.AsSpan())) return false;
            pos = first.Length;
            idx = 1;
        }

        // Suffix anchored setup
        int lastChunkIndex = chunkCount - 1;
        string last = _chunks[lastChunkIndex];
        int suffixStart = s.Length; // exclusive boundary for searching middles

        if (!_endsWithStar)
        {
            if (last.Length == 0) return true; // defensive
            if (last.Length > s.Length) return false;

            int start = s.Length - last.Length;
            if (!MatchAt(s, start, last.AsSpan())) return false;

            suffixStart = start;
            lastChunkIndex--; // last chunk is consumed as suffix
        }

        // Middle chunks in order (including “last” if pattern ends with star)
        for (; idx <= lastChunkIndex; idx++)
        {
            string chunk = _chunks[idx];
            if (chunk.Length == 0) continue;

            int found = IndexOfChunk(s, chunk.AsSpan(), pos, suffixStart);
            if (found < 0) return false;
            pos = found + chunk.Length;
        }

        // If pattern ends with star, we’re good; if it didn’t, suffix already checked.
        // Also, if pattern doesn't start with star, prefix already checked.
        return true;
    }

    private static bool MatchAt(ReadOnlySpan<char> s, int start, ReadOnlySpan<char> chunk)
    {
        if (start < 0 || start + chunk.Length > s.Length) return false;

        for (int i = 0; i < chunk.Length; i++)
        {
            char pc = chunk[i];
            if (pc == '?') continue;
            if (s[start + i] != pc) return false;
        }
        return true;
    }

    // Find chunk between [from, toExclusive], supporting '?'
    private static int IndexOfChunk(ReadOnlySpan<char> s, ReadOnlySpan<char> chunk, int from, int toExclusive)
    {
        if (chunk.Length == 0) return from;
        int maxStart = toExclusive - chunk.Length;
        if (maxStart < from) return -1;

        for (int i = from; i <= maxStart; i++)
        {
            if (MatchAt(s, i, chunk))
                return i;
        }
        return -1;
    }

    public override string ToString() => _pattern;
}
