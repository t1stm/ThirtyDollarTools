using System.Reflection;
using OpenTK.Mathematics;
using Serilog;
using ThirtyDollarConverter.Editor;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management;
using EditorScene.Scenes.Dialogs;

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

    /// <summary>
    ///     Builds a Selected icon directly - the normal path goes through the atlas store,
    ///     which has no images in tests.
    /// </summary>
    private static Panel NewIcon(SoundPicker picker, InstrumentSound instance)
    {
        return NewIcon(picker, instance.Sound, instance);
    }

    /// <summary>A null instance builds an Available icon, one for a sound not added yet.</summary>
    private static Panel NewIcon(SoundPicker picker, string sound, InstrumentSound? instance)
    {
        var type = typeof(SoundPicker).GetNestedType("SoundIcon", BindingFlags.NonPublic)!;
        return (Panel)Activator.CreateInstance(type,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [picker.Context, picker, sound, instance], null)!;
    }

    /// <summary>Scrolls a fresh icon for the instance with the given modifiers held.</summary>
    private static void ScrollIcon(SoundPicker picker, InstrumentSound instance, float notches,
        bool ctrl = false, bool shift = false)
    {
        picker.CtrlHeld = ctrl;
        picker.ShiftHeld = shift;

        var icon = NewIcon(picker, instance);
        icon.GetType().GetMethod("HandleScroll")!.Invoke(icon, [new Vector2(0, notches)]);
    }

    /// <summary>An icon carrying its value/volume/pan label, as Selected icons do with adjustments on.</summary>
    private static Panel NewAdjustableIcon(SoundPicker picker, InstrumentSound instance)
    {
        var icon = NewIcon(picker, instance);
        icon.Width = 40;
        icon.Height = 40;
        icon.GetType().GetMethod("EnableAdjustmentText")!.Invoke(icon, null);
        return icon;
    }

    [Fact]
    public void HandleScroll_CtrlShift_StepsTheValueByATenth()
    {
        var picker = NewPicker(new EditorTestContext());
        picker.SetSelected(["kick"]);
        var instance = picker.Instances[0];

        for (var i = 0; i < 3; i++) ScrollIcon(picker, instance, 1, true, true);
        Assert.Equal(0.3, instance.Value); // exact: repeated 0.1 steps are rounded back

        ScrollIcon(picker, instance, -1, true, true);
        Assert.Equal(0.2, instance.Value);

        // The plain and single-modifier modes are unaffected by the new branch.
        ScrollIcon(picker, instance, 1);
        Assert.Equal(1.2, instance.Value);
        ScrollIcon(picker, instance, 1, ctrl: true);
        Assert.Equal(105, instance.Volume);
        ScrollIcon(picker, instance, 1, shift: true);
        Assert.Equal(5, instance.Pan);
    }

    [Fact]
    public void AddInstance_Duplicating_CopiesTheTuningAndLandsRightAfterTheSource()
    {
        // Right-clicking a selected sound duplicates it, so one instrument can play it
        // twice with different tuning (dual-octave playback).
        var picker = NewPicker(new EditorTestContext());
        picker.SetSelected(["kick", "clap"]);
        picker.Instances[0].Value = -5;

        var duplicate = picker.AddInstance("kick", picker.Instances[0]);

        Assert.Equal(["kick", "kick", "clap"], picker.Instances.Select(instance => instance.Sound));
        Assert.Equal(-5, duplicate.Value);
        Assert.NotSame(picker.Instances[0], duplicate);
        Assert.Equal(["kick", "clap"], picker.Selected); // names dedupe
    }

    [Fact]
    public void AdjustmentLabel_FollowsItsIcon_WhenTheGridMovesIt()
    {
        // Duplicating inserts an icon mid-grid and shifts the ones after it; each label has
        // to move with its icon instead of staying behind over the new neighbour's.
        var picker = NewPicker(new EditorTestContext());
        var grid = new FlexPanel(picker.Context) { Width = 500, Height = 100 };
        var shifted = NewAdjustableIcon(picker, new InstrumentSound { Sound = "clap", Value = -12 });
        grid.AddChild(shifted);
        grid.Layout();

        grid.Children = [NewAdjustableIcon(picker, new InstrumentSound { Sound = "kick" }), shifted];
        grid.Layout();

        var label = Assert.Single(shifted.Children);
        Assert.True(shifted.Computed.AbsoluteX > 0);
        Assert.Equal(shifted.Computed.AbsoluteX, label.Computed.AbsoluteX);
    }

    [Fact]
    public void RightPress_OnAnAvailableIcon_PreviewsItWithoutAddingIt()
    {
        var picker = NewPicker(new EditorTestContext());
        InstrumentSound? previewed = null;
        picker.OnPreviewSound = sound => previewed = sound;
        var icon = NewIcon(picker, "kick", null);

        var handled = (bool)icon.GetType().GetMethod("HandleRightPress")!.Invoke(icon, [0f, 0f])!;

        Assert.True(handled);
        Assert.Equal("kick", previewed?.Sound);
        Assert.Empty(picker.Instances);
    }

    [Fact]
    public void RemoveInstance_DropsOnlyThatCopy()
    {
        var picker = NewPicker(new EditorTestContext());
        picker.SetSelected(["kick"]);
        var duplicate = picker.AddInstance("kick", picker.Instances[0]);
        duplicate.Value = -12;

        picker.RemoveInstance(duplicate);

        var remaining = Assert.Single(picker.Instances);
        Assert.Equal("kick", remaining.Sound);
        Assert.Equal(0, remaining.Value);
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
        // The icon grid must stay a plain wrap of icons - the hint sits in the outer,
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