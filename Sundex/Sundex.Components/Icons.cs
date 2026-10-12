namespace Sundex.Components;

/// <summary>
///     The icon glyphs the UI uses, as codepoints in the bundled <c>"Lucide"</c> font. Draw them with
///     a label built with <c>font: Icons.Font</c>.
///     <para>
///         Codepoints come from <c>lucide-static@1.55.0</c>, <c>font/info.json</c>. Lucide renumbers
///         between releases, so re-read them from that file whenever <c>lucide.ttf</c> is replaced.
///     </para>
/// </summary>
public static class Icons
{
    public const string Font = "Lucide";

    public const string Play = "";
    public const string Copy = "";
    public const string X = "";
    public const string Search = "";

    /// <summary>Every icon above, for tests that check each one renders.</summary>
    public static readonly (string Name, string Glyph)[] All =
    [
        (nameof(Play), Play),
        (nameof(Copy), Copy),
        (nameof(X), X),
        (nameof(Search), Search)
    ];
}
