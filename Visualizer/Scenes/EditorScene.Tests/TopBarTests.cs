using EditorScene.Scenes.Components;
using EditorScene.Scenes.Layout;
using EditorScene.State;
using Sundex.Components.Labels;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Markup;

namespace EditorScene.Tests;

/// <summary>
///     The top bar's timing and states. The motion was signed off in the design canvas as CSS, so
///     the numbers here are the canvas's: a curve that drifts or a hold that changes length is a
///     regression even though nothing throws.
/// </summary>
public class TopBarTests
{
    private readonly EditorTestContext _context = new();

    /// <summary>The tool switch's clock, in seconds.</summary>
    private double _now;

    /// <summary>A label shortened in place keeps its slice's length, padded with NULs.</summary>
    private static string Text(Button button)
    {
        return button.Label.Value.ToString().TrimEnd('\0');
    }

    [Fact]
    public void CubicBezier_MatchesCssEase()
    {
        Assert.Equal(0f, CubicBezier.Ease.At(0f));
        Assert.Equal(1f, CubicBezier.Ease.At(1f));
        // cubic-bezier(.25, .1, .25, 1) halfway through its time.
        Assert.Equal(0.8024f, CubicBezier.Ease.At(0.5f), 3);
    }

    [Fact]
    public void Transition_RetargetedMidway_StartsFromWhereItIs()
    {
        var value = new Transition(0);
        value.Set(1, 0, 1, new CubicBezier(0, 0, 1, 1));
        Assert.Equal(0.5f, value.Value(0.5f), 3);

        value.Set(0, 0.5f, 1, new CubicBezier(0, 0, 1, 1));
        Assert.Equal(0.5f, value.Value(0.5f), 3);
        Assert.Equal(0.25f, value.Value(1f), 3);
        Assert.False(value.Running(1.5f));
    }

    /// <summary>
    ///     A pick opens the name, holds it until 1.3 s, and folds it back over 0.26 s; picking the
    ///     other tool meanwhile folds the first one at once.
    /// </summary>
    [Fact]
    public void ToolSwitch_PeekOpensTheName_HoldsIt_AndFoldsItBack()
    {
        var tools = EditorTestContext.Styled(new ToolSwitch(_context, () => _now));
        tools.SetActive(EditorTool.Draw);

        tools.Peek(EditorTool.Draw);
        tools.Animate();
        Assert.Equal(0f, tools.NameWidth(EditorTool.Draw));

        _now = 0.05;
        tools.Animate();
        var opening = tools.NameWidth(EditorTool.Draw);

        _now = 0.5;
        tools.Animate();
        var open = tools.NameWidth(EditorTool.Draw);
        Assert.InRange(opening, 0.01f, open - 0.01f);

        _now = 1.25;
        tools.Animate();
        Assert.Equal(open, tools.NameWidth(EditorTool.Draw));

        _now = 1.3 + 0.26 + 0.01;
        tools.Animate();
        Assert.Equal(0f, tools.NameWidth(EditorTool.Draw));

        _now = 2;
        tools.Peek(EditorTool.Draw);
        _now = 2.5;
        tools.Animate();
        tools.Peek(EditorTool.Select);
        _now = 3;
        tools.Animate();
        Assert.Equal(0f, tools.NameWidth(EditorTool.Draw));
        Assert.True(tools.NameWidth(EditorTool.Select) > 0, "Select's name never opened");
    }

    /// <summary>
    ///     A project with no file saves as; New and Open show only until the first edit; Save's
    ///     chip is lit exactly while there are unsaved changes.
    /// </summary>
    [Fact]
    public void Header_FollowsTheProjectsFileAndEdits()
    {
        var markup = _context.AssetProvider.LoadString("Scenes/Layout/Editor Header/EditorHeader.snx.xml");
        var header = new SundexContext(_context).NewComponent(markup);
        header.Element.ApplyStyleSheet(EditorTestContext.Styles);

        var state = new EditorState();
        var bar = new EditorHeaderController(header, state, new ToolSwitch(_context));
        var save = header.GetID<Button>("save-button");
        var files = header.GetID<Sundex.Components.Panels.Panel>("file-slot");
        bool Lit() => save.Children[0].Classes.Contains("key-chip-lit");

        bar.Update();
        Assert.Equal("Save As", Text(save));
        Assert.True(files.Visible);
        Assert.False(Lit());

        state.AddTrack();
        bar.Update();
        Assert.Equal("Save As", Text(save));
        Assert.False(files.Visible);
        Assert.True(Lit());

        var path = Path.Combine(Path.GetTempPath(), $"topbar-{Guid.NewGuid():N}.tdwproj");
        try
        {
            state.SaveProjectToFile(path);
            bar.Update();
            Assert.Equal("Save", Text(save));
            Assert.False(files.Visible);
            Assert.False(Lit());
        }
        finally
        {
            File.Delete(path);
        }

        state.NewProject();
        bar.Update();
        Assert.Equal("Save As", Text(save));
        Assert.True(files.Visible);
    }
}
