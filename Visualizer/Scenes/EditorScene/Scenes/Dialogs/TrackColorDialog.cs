using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The track recolor grid (ModalLayer content): one swatch per palette entry plus the
///     default fill, applied on a single click - there is nothing to confirm, and a backdrop
///     click leaves the track as it was. Reached from a track's right-click menu and from
///     the inspector's Color row, both handing it the same palette. The grid is generated
///     from that palette rather than from a .snx.xml tree. Pure view - the owner decides
///     what a pick does and closes the modal itself. Tracks that share a color have it ringed;
///     a mix rings nothing, counts the tracks on each color in use and names the colors.
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

    /// <param name="subject">What is being recolored, for the header's chip: a track's name or "2 tracks".</param>
    /// <param name="palette">The pickable colors - the arrangement's clip palette.</param>
    /// <param name="defaultColor">The fill a track with no color of its own takes.</param>
    /// <param name="current">Each recolored track's index into <paramref name="palette" />, null for the default.</param>
    public TrackColorDialog(UIContext context, string subject, IReadOnlyList<Vector4> palette,
        Vector4 defaultColor, IReadOnlyList<int?> current)
    {
        var inUse = current.CountBy(index => index ?? -1).ToDictionary();
        var mixed = inUse.Count > 1;

        var grid = new FlexPanel(context) { Classes = ["color-grid"] };
        grid.AddChild(Swatch(context, "Default", defaultColor, null));
        for (var i = 0; i < palette.Count; i++)
        {
            var index = i; // captured per swatch, not per loop
            grid.AddChild(Swatch(context, NameOf(index), palette[index], index));
        }

        Element = new FlexPanel(context)
        {
            ID = "track-color-dialog",
            Classes = ["dialog-frame"],
            Children = [DialogHeader.Build(context, "Track color", subject), grid]
        };

        if (mixed)
        {
            var names = inUse.Keys.Order().Select(key => key < 0 ? "Default" : NameOf(key)).ToList();
            var all = current.Count == 2 ? "both" : $"all {current.Count}";
            Element.Children.Insert(1, new Label(context,
                    $"Mixed: {string.Join(", ", names[..^1])} and {names[^1]}. Picking one recolors {all}.")
                { Classes = ["caption-label", "track-color-caption"] });
            Element.Children = Element.Children; // re-parent the inserted caption
        }

        return;

        UIElement Swatch(UIContext ctx, string name, Vector4 color, int? index)
        {
            var key = index ?? -1;
            var tracks = inUse.GetValueOrDefault(key);
            return Option(ctx, name, color, !mixed && tracks > 0, mixed ? tracks : 0, index);
        }
    }

    private static string NameOf(int index) => index < Names.Length ? Names[index] : $"Color {index + 1}";

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    /// <summary>Fired with the picked palette index, or null for the default fill.</summary>
    public Action<int?>? OnPick { get; set; }

    /// <param name="count">How many of a mixed selection's tracks have this color, shown on the chip; 0 for none.</param>
    private UIElement Option(UIContext context, string name, Vector4 color, bool selected, int count, int? index)
    {
        // The fill is a palette entry, so it is set here rather than in the sheet;
        // `color-chip` only shapes the box and declares no background, so the cascade at
        // mount leaves this one alone.
        var chip = new Panel(context)
        {
            Classes = ["color-chip"],
            Background = new ColoredPlane { Color = color }
        };
        if (count > 0)
            chip.AddChild(new FlexPanel(context)
            {
                Classes = ["color-count"],
                X = 4,
                Y = 4,
                Children = [new Label(context, count.ToString()) { Classes = ["color-count-label"] }]
            });

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
