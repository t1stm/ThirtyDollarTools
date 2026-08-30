using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The track recolor grid (ModalLayer content): one swatch per palette entry plus the
///     default fill, applied on a single click - there is nothing to confirm, and a backdrop
///     click leaves the track as it was. Reached from a track's right-click menu and from
///     the inspector's Color row, both handing it the same palette. The grid is generated
///     from that palette rather than from a .snx.xml tree. Pure view - the owner decides
///     what a pick does and closes the modal itself.
/// </summary>
public sealed class TrackColorDialog
{
    /// <summary>
    ///     Display names for Theme.snx.ss's clip_palette, in its order, so the grid is not a
    ///     set of unlabeled squares. Palette entries past the end of this list fall back to
    ///     their number, so the two need not stay the same length.
    /// </summary>
    private static readonly string[] Names =
        ["Purple", "Green", "Orange", "Rose", "Teal", "Olive", "Violet"];

    /// <param name="palette">The pickable colors - the arrangement's clip palette.</param>
    /// <param name="defaultColor">The fill a track with no color of its own takes.</param>
    /// <param name="current">The track's current index into <paramref name="palette" />, or null for the default.</param>
    public TrackColorDialog(UIContext context, string trackName, IReadOnlyList<Vector4> palette,
        Vector4 defaultColor, int? current)
    {
        var grid = new FlexPanel(context) { Classes = ["color-grid"] };
        grid.AddChild(Swatch(context, "Default", defaultColor, current is null, null));
        for (var i = 0; i < palette.Count; i++)
        {
            var index = i; // captured per swatch, not per loop
            grid.AddChild(Swatch(context, index < Names.Length ? Names[index] : $"Color {index + 1}",
                palette[index], current == index, index));
        }

        Element = new FlexPanel(context)
        {
            ID = "track-color-dialog",
            Classes = ["dialog-frame"],
            Children =
            [
                new Label(context, $"Color for “{trackName}”") { Classes = ["title-label"] },
                grid
            ]
        };
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    /// <summary>Fired with the picked palette index, or null for the default fill.</summary>
    public Action<int?>? OnPick { get; set; }

    private UIElement Swatch(UIContext context, string name, Vector4 color, bool selected, int? index)
    {
        // The fill is a palette entry, so it is set here rather than in the sheet;
        // `color-chip` only shapes the box and declares no background, so the cascade at
        // mount leaves this one alone.
        var chip = new Panel(context)
        {
            Classes = ["color-chip"],
            Background = new ColoredPlane { Color = color }
        };

        // The selected option's fill sits behind its name too, so the name brightens with it
        // instead of staying muted against the selection shade.
        var label = new Label(context, name)
        {
            Classes = selected ? ["color-option-name", "color-option-name-selected"] : ["color-option-name"]
        };

        return new FlexPanel(context)
        {
            Classes = selected ? ["color-option", "color-option-selected"] : ["color-option"],
            Cursor = CursorType.Pointer,
            Children = [chip, label],
            OnClick = _ => OnPick?.Invoke(index)
        };
    }
}
