using OpenTK.Mathematics;
using SettingsScene.Scenes;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Style.DSL.Abstract.Values;
using VisualizerScene.Settings;

namespace SettingsScene.Tests;

/// <summary>
///     Hot reload against a screen as it actually ships, rather than a hand-built tree: the
///     settings interface has a stylesheet with imports, code-built rows added after the
///     style pass, and a preview strip whose tiles are sized from settings rather than from
///     the sheet. Reloading its styles has to leave all of that standing.
/// </summary>
public class StyleReloadTests
{
    private readonly SettingsTestContext _context = new();

    private SettingsInterface Build()
    {
        return new SettingsInterface(_context, new VisualizerSettings(), () => { });
    }

    private static Vector4 FillOf(UIElement element)
    {
        return ((ColoredPlane)((Panel)element).Background!).Color;
    }

    [Fact]
    public void ReloadingStylesKeepsTheSheetApplied()
    {
        var tracked = UIElement.TrackPristineStyles;
        UIElement.TrackPristineStyles = true;
        try
        {
            var ui = Build();
            var stage = ui.Component.GetID<Panel>("stage");
            var before = FillOf(stage);

            // Nothing on disk changed, so the reload is a no-op in effect - which is exactly
            // what has to be true. A reload that reverts styles it then fails to re-apply
            // would show up here as the stage losing its fill.
            ui.Component.ReloadStyleSheet();

            Assert.Equal(before, FillOf(stage));
            Assert.Equal(new ColorValue("#0e0f16").Vector, FillOf(stage));
        }
        finally
        {
            UIElement.TrackPristineStyles = tracked;
        }
    }

    [Fact]
    public void ReloadingStylesIsRepeatable()
    {
        var tracked = UIElement.TrackPristineStyles;
        UIElement.TrackPristineStyles = true;
        try
        {
            var ui = Build();
            var stage = ui.Component.GetID<Panel>("stage");

            for (var i = 0; i < 3; i++) ui.Component.ReloadStyleSheet();

            Assert.Equal(new ColorValue("#0e0f16").Vector, FillOf(stage));
        }
        finally
        {
            UIElement.TrackPristineStyles = tracked;
        }
    }

    [Fact]
    public void ReloadingStylesKeepsCodeBuiltRows()
    {
        // The sections are added to the tree after the markup was styled, and are styled by
        // Panel.AddChild rather than by the document's own pass. A reload must not drop them.
        var ui = Build();
        var rowsBefore = ui.SettingsList.Children.Count;

        ui.Component.ReloadStyleSheet();

        Assert.Equal(rowsBefore, ui.SettingsList.Children.Count);
        Assert.NotEqual(0, rowsBefore);
    }

    [Fact]
    public void ReloadingStylesKeepsTileSizesTheSettingsChose()
    {
        // The preview tiles take their size from EventSize, assigned from code well after
        // the style pass. This is the case pristine-tracking has to leave alone: the sheet
        // never writes width on a tile, so a reload must not revert it.
        var tracked = UIElement.TrackPristineStyles;
        UIElement.TrackPristineStyles = true;
        try
        {
            var settings = new VisualizerSettings { EventSize = 96, LineAmount = 4 };
            var ui = new SettingsInterface(_context, settings, () => { });
            var tile = ui.Strip.Children[0];

            ui.Component.ReloadStyleSheet();

            Assert.Equal(96f, tile.Width.Value);
        }
        finally
        {
            UIElement.TrackPristineStyles = tracked;
        }
    }
}
