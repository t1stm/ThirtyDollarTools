using OpenTK.Mathematics;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Shared Tokyo-Night-derived colors for the editor's code-built UI (code-built
///     children never receive the stylesheet, so these are set inline rather than via
///     .ss classes). Consolidates values that were re-declared — and kept in sync only
///     by comment — across many files. Not a full design system: shades unique to one
///     view (grid lines, row highlights that nothing else reuses) stay local to it.
/// </summary>
internal static class EditorPalette
{
    public static readonly Vector4 Panel = new(0.086f, 0.086f, 0.118f, 1f); // #16161e
    public static readonly Vector4 Surface = new(0.16f, 0.18f, 0.26f, 1f); // #292e42
    public static readonly Vector4 SurfaceRaised = new(0.21f, 0.23f, 0.33f, 1f); // #353a54
    public static readonly Vector4 InputBackground = new(0.15f, 0.16f, 0.21f, 1f);
    public static readonly Vector4 Divider = new(0.2f, 0.204f, 0.29f, 1f); // #33344a
    public static readonly Vector4 DividerHover = new(0.247f, 0.255f, 0.376f, 1f); // #3f4160
    public static readonly Vector4 Accent = new(0.30f, 0.42f, 0.80f, 1f); // #4c6bcc
    public static readonly Vector4 TextMuted = new(0.337f, 0.373f, 0.537f, 1f); // #565f89
    public static readonly Vector4 TextDim = new(0.66f, 0.7f, 0.86f, 1f);
    public static readonly Vector4 Header = new(0.478f, 0.635f, 0.968f, 1f); // #7aa2f7
    public static readonly Vector4 SelectionHighlight = new(0.61f, 0.75f, 1f, 1f); // #9bc0ff
    public static readonly Vector4 Playhead = new(0.75f, 0.79f, 0.96f, 1f); // #c0caf5
    public static readonly Vector4 DangerAccent = new(0.969f, 0.463f, 0.557f, 1f); // #f7768e
}
