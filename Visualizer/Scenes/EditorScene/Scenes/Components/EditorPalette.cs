using OpenTK.Mathematics;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract.Values;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The editor's colors, loaded from the stylesheet by <see cref="Apply" /> at startup.
///     <para>
///         These are the ones no selector can reach: they paint raw renderables in draw
///         code - grid lines batched into one instanced call, pooled blocks reassigned
///         every layout, fills swapped on a state change - rather than
///         <c>[NamedSetting]</c> properties on a styled element. Everything that IS an
///         element property is styled by class/id instead and never appears here.
///     </para>
///     <para>
///         The values below are fallbacks, used only if a sheet omits a token; the real
///         ones live in Scenes/Styles/Palette.snx.ss and Scenes/Views/GridViews.snx.ss.
///     </para>
/// </summary>
internal static class EditorPalette
{
    public static Vector4 Panel { get; private set; } = new(0.086f, 0.086f, 0.118f, 1f);
    public static Vector4 Surface { get; private set; } = new(0.16f, 0.18f, 0.26f, 1f);
    public static Vector4 SurfaceRaised { get; private set; } = new(0.21f, 0.23f, 0.33f, 1f);
    public static Vector4 InputBackground { get; private set; } = new(0.15f, 0.16f, 0.21f, 1f);
    public static Vector4 Divider { get; private set; } = new(0.2f, 0.204f, 0.29f, 1f);
    public static Vector4 DividerHover { get; private set; } = new(0.247f, 0.255f, 0.376f, 1f);
    public static Vector4 Accent { get; private set; } = new(0.30f, 0.42f, 0.80f, 1f);
    public static Vector4 AccentYellow { get; private set; } = new(0.878f, 0.686f, 0.406f, 1f);
    public static Vector4 TextMuted { get; private set; } = new(0.337f, 0.373f, 0.537f, 1f);
    public static Vector4 TextDim { get; private set; } = new(0.66f, 0.7f, 0.86f, 1f);
    public static Vector4 Header { get; private set; } = new(0.478f, 0.635f, 0.968f, 1f);
    public static Vector4 SelectionHighlight { get; private set; } = new(0.61f, 0.75f, 1f, 1f);
    public static Vector4 Playhead { get; private set; } = new(0.75f, 0.79f, 0.96f, 1f);
    public static Vector4 DangerAccent { get; private set; } = new(0.969f, 0.463f, 0.557f, 1f);
    public static Vector4 RowSelected { get; private set; } = new(0.255f, 0.282f, 0.408f, 1f);
    public static Vector4 ProgressTrack { get; private set; } = new(0.251f, 0.251f, 0.376f, 1f);

    // The two canvas views' own shades - see Scenes/Views/GridViews.snx.ss.
    public static Vector4 GridBackground { get; private set; } = new(0.067f, 0.07f, 0.1f, 1f);
    public static Vector4 StepLine { get; private set; } = new(0.11f, 0.12f, 0.17f, 1f);
    public static Vector4 RowLine { get; private set; } = new(0.10f, 0.11f, 0.15f, 1f);
    public static Vector4 OctaveLine { get; private set; } = new(0.20f, 0.22f, 0.31f, 1f);
    public static Vector4 ZeroRow { get; private set; } = new(0.10f, 0.11f, 0.16f, 1f);
    public static Vector4 StripSegmentA { get; private set; } = new(0.16f, 0.18f, 0.26f, 1f);
    public static Vector4 StripSegmentB { get; private set; } = new(0.21f, 0.23f, 0.33f, 1f);
    public static Vector4 CutRow { get; private set; } = new(0.21f, 0.23f, 0.33f, 1f);
    public static Vector4 MarqueeFill { get; private set; } = new(0.30f, 0.42f, 0.80f, 0.25f);

    /// <summary>
    ///     Stable per-sound note colors; a sound's name picks its index, so the order here
    ///     is what keeps a project's notes the same color between runs.
    /// </summary>
    public static Vector4[] SoundPalette { get; private set; } =
    [
        new(0.30f, 0.42f, 0.80f, 1f), // blue
        new(0.62f, 0.36f, 0.71f, 1f), // purple
        new(0.24f, 0.60f, 0.46f, 1f), // green
        new(0.78f, 0.47f, 0.25f, 1f), // orange
        new(0.72f, 0.32f, 0.42f, 1f), // rose
        new(0.28f, 0.56f, 0.67f, 1f), // teal
        new(0.66f, 0.58f, 0.28f, 1f), // olive
        new(0.48f, 0.44f, 0.78f, 1f) // violet
    ];

    /// <summary>
    ///     Reads the palette out of the editor's sheet. Call once, before any view that
    ///     paints with these is constructed - they copy the values into renderables, so a
    ///     later change wouldn't reach what is already on screen.
    ///     ponytail: no live reload, no change notification. The sheet is read at startup
    ///     and that's it; add an event here if retuning colors without restarting is worth it.
    /// </summary>
    public static void Apply(StyleSheet? sheet)
    {
        if (sheet is null) return;

        Panel = Color(sheet, "palette", "panel", Panel);
        Surface = Color(sheet, "palette", "surface", Surface);
        SurfaceRaised = Color(sheet, "palette", "surface-raised", SurfaceRaised);
        InputBackground = Color(sheet, "palette", "input-background", InputBackground);
        Divider = Color(sheet, "palette", "divider", Divider);
        DividerHover = Color(sheet, "palette", "divider-hover", DividerHover);
        Accent = Color(sheet, "palette", "accent", Accent);
        AccentYellow = Color(sheet, "palette", "accent-yellow", AccentYellow);
        TextMuted = Color(sheet, "palette", "text-muted", TextMuted);
        TextDim = Color(sheet, "palette", "text-dim", TextDim);
        Header = Color(sheet, "palette", "header", Header);
        SelectionHighlight = Color(sheet, "palette", "selection-highlight", SelectionHighlight);
        Playhead = Color(sheet, "palette", "playhead", Playhead);
        DangerAccent = Color(sheet, "palette", "danger-accent", DangerAccent);
        RowSelected = Color(sheet, "palette", "row-selected", RowSelected);
        ProgressTrack = Color(sheet, "palette", "progress-track", ProgressTrack);

        GridBackground = Color(sheet, "grid-palette", "background", GridBackground);
        StepLine = Color(sheet, "grid-palette", "step-line", StepLine);
        RowLine = Color(sheet, "grid-palette", "row-line", RowLine);
        OctaveLine = Color(sheet, "grid-palette", "octave-line", OctaveLine);
        ZeroRow = Color(sheet, "grid-palette", "zero-row", ZeroRow);
        StripSegmentA = Color(sheet, "grid-palette", "strip-segment-a", StripSegmentA);
        StripSegmentB = Color(sheet, "grid-palette", "strip-segment-b", StripSegmentB);
        CutRow = Color(sheet, "grid-palette", "cut-row", CutRow);
        MarqueeFill = Color(sheet, "grid-palette", "marquee-fill", MarqueeFill);

        // An empty list would make every note-color lookup divide by zero, so a sheet
        // that declares the key but no entries keeps the built-in palette.
        if (sheet.GetStyleValueForTag("sound-palette", "colors") is ArrayValue { Values.Count: > 0 } array)
            SoundPalette = array.Values.OfType<ColorValue>().Select(color => color.Vector).ToArray();
    }

    private static Vector4 Color(StyleSheet sheet, string tag, string property, Vector4 fallback)
    {
        return sheet.GetStyleValueForTag(tag, property) is ColorValue color ? color.Vector : fallback;
    }
}
