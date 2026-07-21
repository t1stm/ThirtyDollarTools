using System.Reflection;
using EditorScene.Scenes.Components;
using Serilog;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management;

namespace EditorScene.Tests;

public class SoundPickerTests
{
    private static SoundPicker NewPicker(EditorTestContext ctx)
    {
        var atlases = new AtlasStore((AssetProvider)ctx.AssetProvider, new LoggerConfiguration().CreateLogger());
        return new SoundPicker(ctx, atlases) { MultiSelect = true, ShowAdjustments = true };
    }

    private static FlexPanel GetField(SoundPicker picker, string name)
    {
        var field = typeof(SoundPicker).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (FlexPanel)field.GetValue(picker)!;
    }

    private static UIElement KeybindNote(SoundPicker picker)
    {
        var field = typeof(SoundPicker).GetField("_keybindNote", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (UIElement)field.GetValue(picker)!;
    }

    [Fact]
    public void KeybindNote_OnlyShowsWithAdjustmentsAndANonEmptySelection()
    {
        var picker = NewPicker(new EditorTestContext());
        var note = KeybindNote(picker);
        var selectedRow = GetField(picker, "_selectedRow");

        picker.SetSelected([]);
        Assert.DoesNotContain(note, selectedRow.Children);

        picker.SetSelected(["kick"]);
        Assert.Contains(note, selectedRow.Children);

        picker.SetSelected([]);
        Assert.DoesNotContain(note, selectedRow.Children);
    }

    [Fact]
    public void KeybindNote_StaysHidden_WithoutShowAdjustments()
    {
        var ctx = new EditorTestContext();
        var atlases = new AtlasStore((AssetProvider)ctx.AssetProvider, new LoggerConfiguration().CreateLogger());
        var picker = new SoundPicker(ctx, atlases) { MultiSelect = true }; // ShowAdjustments left off
        var note = KeybindNote(picker);

        picker.SetSelected(["kick"]);

        Assert.DoesNotContain(note, GetField(picker, "_selectedRow").Children);
    }

    [Fact]
    public void KeybindNote_LivesBesideTheIconGrid_NotInsideIt()
    {
        // The icon grid must stay a plain wrap of icons — the hint sits in the outer,
        // non-wrapping row so icons wrap within the space it leaves, rather than just
        // trailing the last icon inline.
        var picker = NewPicker(new EditorTestContext());
        var note = KeybindNote(picker);
        var selectedGrid = GetField(picker, "_selectedGrid");
        var selectedRow = GetField(picker, "_selectedRow");

        picker.SetSelected(["kick"]);

        Assert.DoesNotContain(note, selectedGrid.Children);
        Assert.Contains(selectedGrid, selectedRow.Children);
        Assert.Contains(note, selectedRow.Children);
        Assert.True(selectedRow.Children.IndexOf(selectedGrid) < selectedRow.Children.IndexOf(note));
    }
}
