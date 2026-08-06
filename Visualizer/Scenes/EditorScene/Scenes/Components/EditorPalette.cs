using OpenTK.Mathematics;
using Sundex.Style.DSL.Abstract.Values;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The editor's colors (Tokyo Night derived).
///     <para>
///         These are the ones no selector can reach: they paint raw renderables in draw
///         code - grid lines batched into one instanced call, pooled blocks reassigned
///         every layout, fills swapped on a state change - rather than
///         <c>[NamedSetting]</c> properties on a styled element. Everything that IS an
///         element property is styled by class/id in Scenes/Styles instead and never
///         appears here.
///     </para>
///     <para>
///         ponytail: constants, not sheet-loaded. Retuning them needs a rebuild. The
///         upgrade path is scoped style imports (<c>import "palette.snx.ss" as pal;</c>
///         then <c>$pal.ACCENT</c>), at which point these come back out as sheet
///         variables shared with the control sheets.
///     </para>
/// </summary>
internal static class EditorPalette
{
    // Chrome: the column/header fill, and the two raised shades above it.
    public static Vector4 Panel { get; } = Hex("#16161e");
    public static Vector4 Surface { get; } = Hex("#292e42");
    public static Vector4 SurfaceRaised { get; } = Hex("#353a54");
    public static Vector4 InputBackground { get; } = Hex("#262936");

    // Hairlines and their hover state (menu buttons, transport fills, rules).
    public static Vector4 Divider { get; } = Hex("#33344a");
    public static Vector4 DividerHover { get; } = Hex("#3f4160");

    // The two accents everything color-codes against.
    public static Vector4 Accent { get; } = Hex("#4c6bcc");
    public static Vector4 AccentYellow { get; } = Hex("#e0af68");

    // Text, dimmest first.
    public static Vector4 TextMuted { get; } = Hex("#565f89");
    public static Vector4 TextDim { get; } = Hex("#a8b3db");
    public static Vector4 Header { get; } = Hex("#7aa2f7");

    // Transport/selection markers.
    public static Vector4 SelectionHighlight { get; } = Hex("#9bc0ff");
    public static Vector4 Playhead { get; } = Hex("#c0caf5");
    public static Vector4 DangerAccent { get; } = Hex("#f7768e");

    // Row fill for the selected track in the track list.
    public static Vector4 RowSelected { get; } = Hex("#414868");

    // The transport progress bar's unfilled track.
    public static Vector4 ProgressTrack { get; } = Hex("#404060");

    // The two canvas views' own shades, kept apart from the chrome above so retuning
    // the grid doesn't disturb it. The canvas is darker than any chrome panel.
    public static Vector4 GridBackground { get; } = Hex("#11121a");

    // Note editor line work, faintest first: every step, then the row lines, the octave
    // (every 12th) emphasis, and the zero row's band.
    public static Vector4 StepLine { get; } = Hex("#1c1f2b");
    public static Vector4 RowLine { get; } = Hex("#1a1c26");
    public static Vector4 OctaveLine { get; } = Hex("#33384f");
    public static Vector4 ZeroRow { get; } = Hex("#1a1c29");

    // Segment strips alternate between these two so boundaries are visible without a
    // separating line.
    public static Vector4 StripSegmentA { get; } = Hex("#292e42");
    public static Vector4 StripSegmentB { get; } = Hex("#353a54");

    // The !cut row pinned under the grid.
    public static Vector4 CutRow { get; } = Hex("#353a54");

    // The drag-select rectangle. Translucent (the trailing 40 is its alpha) so the notes
    // and clips underneath stay readable while the marquee is over them.
    public static Vector4 MarqueeFill { get; } = Hex("#4c6bcc40");

    /// <summary>
    ///     Stable per-sound note colors; a sound's name picks its index, so reordering or
    ///     recoloring entries recolors existing projects' notes - that's the intent, it's
    ///     a palette, not an identity.
    /// </summary>
    public static Vector4[] SoundPalette { get; } =
    [
        Hex("#4c6bcc"), // blue
        Hex("#9e5cb5"), // purple
        Hex("#3d9975"), // green
        Hex("#c77840"), // orange
        Hex("#b8526b"), // rose
        Hex("#478fab"), // teal
        Hex("#a89447"), // olive
        Hex("#7a70c7") // violet
    ];

    // ponytail: ColorValue is the DSL's hex parser and it is already referenced here;
    // the alternative is transcribing 34 colors into float triples by hand.
    private static Vector4 Hex(string hex)
    {
        return new ColorValue(hex).Vector;
    }
}
