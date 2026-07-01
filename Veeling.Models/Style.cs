namespace Veeling.Models;

public enum Tone
{
    Neutral,
    Casual,
    Professional,
    Playful
}

public enum Formality
{
    Informal,
    Neutral,
    Formal
}

public class Style
{
    public Tone Tone { get; init; }

    public Formality Formality { get; init; }

    public string Audience { get; init; } = "general";
}
