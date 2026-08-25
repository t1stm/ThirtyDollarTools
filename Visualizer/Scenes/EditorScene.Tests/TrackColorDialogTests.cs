using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class TrackColorDialogTests
{
    private static readonly Vector4[] Palette =
        [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1)];

    /// <summary>
    ///     The dialog is code-built, not a document with its own &lt;style&gt;, so it takes
    ///     the editor sheet the way EditorInterface's root panel hands it over.
    /// </summary>
    private static (TrackColorDialog dialog, List<UIElement> options) NewDialog(int? current)
    {
        var dialog = new TrackColorDialog(new EditorTestContext(), "Drums", Palette, new Vector4(0.5f), current);
        EditorTestContext.Styled(dialog.Element);
        // The dialog is [title, grid]; the grid is the default swatch, then one per entry.
        var grid = (FlexPanel)dialog.Element.Children[1];
        return (dialog, [.. grid.Children]);
    }

    [Fact]
    public void EachSwatch_ReportsItsOwnPaletteIndex_BehindTheDefault()
    {
        var (dialog, options) = NewDialog(null);
        var picks = new List<int?>();
        dialog.OnPick = index => picks.Add(index);

        Assert.Equal(Palette.Length + 1, options.Count);
        foreach (var option in options) option.OnClick?.Invoke(option);

        // One index per swatch, in palette order, behind the null "Default" entry - the
        // loop must capture each index rather than share the last one.
        Assert.Equal([null, 0, 1, 2], picks);
    }

    [Fact]
    public void ExactlyOneSwatch_IsMarkedAsTheTracksCurrentColor()
    {
        var (_, colored) = NewDialog(1);
        Assert.Contains("color-option-selected", colored[2].Classes); // palette entry 1
        Assert.Equal(1, colored.Count(option => option.Classes.Contains("color-option-selected")));

        var (_, uncolored) = NewDialog(null);
        Assert.Contains("color-option-selected", uncolored[0].Classes); // "Default"
        Assert.Equal(1, uncolored.Count(option => option.Classes.Contains("color-option-selected")));
    }

    [Fact]
    public void SwatchesFitTheDialogsInnerWidth_ThreeToARow()
    {
        var (dialog, options) = NewDialog(null);
        dialog.Element.Layout();

        var grid = (FlexPanel)dialog.Element.Children[1];
        var innerWidth = dialog.Element.Width.Value - 2 * dialog.Element.Padding;
        Assert.True(3 * options[0].Computed.Width + 2 * grid.Spacing <= innerWidth,
            $"three {options[0].Computed.Width}-wide swatches overflow the {innerWidth} inner width");
    }
}
