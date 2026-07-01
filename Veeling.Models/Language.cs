using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Veeling.Models;

public abstract class LanguageException : Exception
{
    public LanguageException(string languageCode, string message) : base(message)
    {
        LanguageCode = languageCode;
    }

    public string LanguageCode { get; private set; }
}

public class InvalidLanguageException(string languageCode)
    : LanguageException(languageCode, $"Invalid language '{languageCode}'.")
{
}

public class LanguageNotEnabledException(string languageCode)
    : LanguageException(languageCode, $"Language '{languageCode}' is not enabled in your project.")
{
}

public class Language : IYamlConvertible
{
    private static readonly Lazy<string[]> codes = new(
        () => [..
            CultureInfo
                .GetCultures(CultureTypes.NeutralCultures)
                .Select(c => c.TwoLetterISOLanguageName)
                .Where(code => code.Length == 2 && code.All(char.IsLetter))
                .Distinct()
                .OrderBy(code => code)]
    );

    public Language(string code)
    {
        if (!IsSupportedLanguage(code))
        {
            throw new ArgumentException($"Invalid language code: {code}", nameof(code));
        }
        Code = code;
    }

    public Language()
    {
        Code = string.Empty;
    }

    public string Code { get; private set; }

    public string GetName() => GetName(this);

    public string GetLongName() => $"{GetName()} ({Code})";

    public override string ToString() => Code;

    public static implicit operator Language(string code)
    {
        try
        {
            return new(code);
        }
        catch (ArgumentException)
        {
            throw new InvalidLanguageException(code);
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is Language other)
        {
            return Code == other.Code;
        }
        return base.Equals(obj);
    }

    public override int GetHashCode() => Code.GetHashCode();

    public static bool IsSupportedLanguage(string code)
    {
        return codes.Value.Contains(code);
    }

    public static string GetName(Language language)
    {
        return new CultureInfo(language.Code).EnglishName;
    }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        var code = scalar.Value;
        if (!IsSupportedLanguage(code))
        {
            throw new InvalidLanguageException(code);
        }
        Code = code;
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        emitter.Emit(new Scalar(Code));
    }
}
