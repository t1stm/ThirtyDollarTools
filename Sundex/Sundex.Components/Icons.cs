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
    public const string Pencil = "";
    public const string Trash2 = "";
    public const string ArrowUp = "";
    public const string House = "";
    public const string ChevronRight = "";
    public const string Folder = "";
    public const string FileMusic = "";
    public const string FileText = "";
    public const string AudioWaveform = "";
    public const string Plus = "";
    public const string Info = "";
    public const string TriangleAlert = "";
    public const string Volume2 = "";
    public const string KeyboardMusic = "";
    public const string ListMusic = "";
    public const string Check = "";
    public const string ChevronDown = "";
    public const string MousePointer2 = "";
    public const string Drum = "";
    public const string Music = "";
    public const string SearchX = "";
    public const string VolumeX = "";
    public const string Target = "";

    /// <summary>Every icon above, for tests that check each one renders.</summary>
    public static readonly (string Name, string Glyph)[] All =
    [
        (nameof(Play), Play),
        (nameof(Copy), Copy),
        (nameof(X), X),
        (nameof(Search), Search),
        (nameof(Pencil), Pencil),
        (nameof(Trash2), Trash2),
        (nameof(ArrowUp), ArrowUp),
        (nameof(House), House),
        (nameof(ChevronRight), ChevronRight),
        (nameof(Folder), Folder),
        (nameof(FileMusic), FileMusic),
        (nameof(FileText), FileText),
        (nameof(AudioWaveform), AudioWaveform),
        (nameof(Plus), Plus),
        (nameof(Info), Info),
        (nameof(TriangleAlert), TriangleAlert),
        (nameof(Volume2), Volume2),
        (nameof(KeyboardMusic), KeyboardMusic),
        (nameof(ListMusic), ListMusic),
        (nameof(Check), Check),
        (nameof(ChevronDown), ChevronDown),
        (nameof(MousePointer2), MousePointer2),
        (nameof(Drum), Drum),
        (nameof(Music), Music),
        (nameof(SearchX), SearchX),
        (nameof(VolumeX), VolumeX),
        (nameof(Target), Target)
    ];
}
