using EditorScene.Scenes;
using EditorScene.State;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

namespace EditorScene.Tests;

/// <summary>
///     1080x720 is the editor's required minimum window size, and the three ways it can break
///     there are arithmetic, not rendering: a tool bar whose fixed-width children add up to
///     more than the grid area, a palette box left narrower than its own widest row, and a
///     hint legend far wider than the bar holding it. All three are checkable without GL -
///     see docs/handover/editor-min-resolution.md.
/// </summary>
public class MinResolutionLayoutTests
{
    /// <summary>The grid area at the minimum window size: 1080 - TrackColumnWidth(260) - PanelWidth(300).</summary>
    private const float GridWidthAt1080 = 520;

    private readonly EditorTestContext _context = new();

    private FlexPanel Panel(string document, string id)
    {
        var markup = new SundexContext(_context);
        // The grid views need an EditorState and a GL context; the bar above them doesn't.
        foreach (var tag in new[] { "track-editor-view", "faithful-palette", "faithful-sequence" })
            markup.RegisterElementFactory(tag, ctx => new Panel(ctx));

        var source = _context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = $"Scenes/{document}.snx.xml", Storage = StorageLocation.Assembly }
        }).Value;

        return markup.NewComponent(source).GetID<FlexPanel>(id);
    }

    private static FlexPanel ToolBar(FlexPanel panel)
    {
        return (FlexPanel)Assert.Single(panel.Children, child => child.Classes.Contains("tool-bar"));
    }

    /// <summary>
    ///     The note editor's bar holds the most: back, name, instrument and both tools. Its
    ///     children have to end inside it - overflow isn't clipped here, so a child past the
    ///     right edge paints over the inspector column and, being drawn later, eats its clicks.
    /// </summary>
    [Theory]
    [InlineData("Layout/Track Editor Panel/TrackEditorPanel", "track-editor-panel")]
    [InlineData("Layout/Faithful Panel/FaithfulPanel", "faithful-panel")]
    public void ToolBarChildren_StayInsideTheGridArea_AtTheMinimumWidth(string document, string id)
    {
        var panel = Panel(document, id);
        panel.Width = GridWidthAt1080;
        panel.Height = 620;
        panel.Layout();

        var bar = ToolBar(panel);
        var right = bar.Computed.AbsoluteX + bar.Computed.Width - bar.Padding;

        foreach (var child in bar.Children)
        {
            var edge = child.Computed.AbsoluteX + child.Computed.Width;
            Assert.True(edge <= right + 0.5f,
                $"{child.Tag}#{child.ID} ends at {edge}, past the bar's {right}");
        }
    }

    /// <summary>
    ///     And the bar still has to be usable: the name field is the percent-width child that
    ///     absorbs the slack, so a fix that stops the overflow by collapsing it to nothing has
    ///     only moved the problem.
    /// </summary>
    [Fact]
    public void OpenedTrackName_KeepsAUsableWidth_AtTheMinimumWidth()
    {
        var panel = Panel("Layout/Track Editor Panel/TrackEditorPanel", "track-editor-panel");
        panel.Width = GridWidthAt1080;
        panel.Height = 620;
        panel.Layout();

        var name = Assert.Single(ToolBar(panel).Children, child => child.ID == "opened-track-name");
        Assert.True(name.Computed.Width >= 60, $"the track name field collapsed to {name.Computed.Width}");
    }

    /// <summary>
    ///     The palette band's two boxes at the minimum width. The Actions box cannot take a
    ///     fixed share at every window size, or the Instruments box is left with the remainder -
    ///     narrower at 1080 than its own "+ New instrument" row, whose label then paints outside
    ///     the button on both sides.
    /// </summary>
    [Fact]
    public void FaithfulPaletteBand_LeavesInstrumentsRoomForItsWidestRow_AtTheMinimumWidth()
    {
        var band = EditorTestContext.Styled(new FlexPanel(_context) { Classes = ["faithful-palette"] });
        var instruments = new FlexPanel(_context) { Classes = ["faithful-section", "faithful-section-instruments"] };
        var actions = new FlexPanel(_context) { Classes = ["faithful-section", "faithful-section-actions"] };
        band.Children = [instruments, actions];

        // The faithful body's inner width at 1080: the grid area less its 14 px padding.
        band.Width = GridWidthAt1080 - 28;
        band.Height = 240;
        band.Layout();

        var row = EditorTestContext.Styled(new Button(_context, "+ New instrument") { Classes = ["menu-row"] });
        row.Layout();

        var room = instruments.Computed.Width - 2 * instruments.Padding;
        Assert.True(room >= row.Label.Computed.Width,
            $"Instruments is {room} px wide inside its padding, its widest row {row.Label.Computed.Width}");
        Assert.True(actions.Computed.Width > 0, "the Actions box collapsed");
        Assert.True(instruments.Computed.Width + actions.Computed.Width + band.Spacing <= band.Computed.Width + 0.5f,
            "the two boxes together overflow the band");
    }

    /// <summary>
    ///     The hint bar's wrap. A Label neither wraps nor clips, so an unbroken legend paints
    ///     across the inspector and off the window, losing the gestures the legend is the only
    ///     documentation of.
    /// </summary>
    [Fact]
    public void WrapHint_BreaksOnSpaces_AndKeepsEveryWord()
    {
        const string text = "Click the palette to add, right-click to preview  •  Draw: click a slot to remove it";
        var lines = EditorInterface.WrapHint(text, 30, 3);

        Assert.True(lines.Count > 1, "a legend well over the budget stayed on one line");
        Assert.Equal(text.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            lines.SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        // Every line but the last has to be inside the budget; the last takes the remainder
        // rather than dropping it.
        foreach (var line in lines.Take(lines.Count - 1)) Assert.True(line.Length <= 30, line);
    }

    [Fact]
    public void WrapHint_NeverExceedsItsLineCount()
    {
        var lines = EditorInterface.WrapHint(string.Join(' ', Enumerable.Repeat("gesture", 200)), 20, 3);
        Assert.Equal(3, lines.Count);
    }
}
