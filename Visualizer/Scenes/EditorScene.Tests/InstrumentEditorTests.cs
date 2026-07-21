using EditorScene.Scenes.Components;
using Serilog;
using Shared.Atlases;
using Sundex.Engine.Asset_Management;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class InstrumentEditorTests
{
    private static InstrumentEditor NewEditor(EditorTestContext ctx)
    {
        var atlases = new AtlasStore((AssetProvider)ctx.AssetProvider, new LoggerConfiguration().CreateLogger());
        return new InstrumentEditor(ctx, atlases);
    }

    [Fact]
    public void Load_PreFillsTheNameAndTheSoundSelection()
    {
        var editor = NewEditor(new EditorTestContext());

        editor.Load("Layer", ["kick", "clap"]);

        Assert.Equal("Layer", editor.NameInput.Value);
        Assert.Equal(new HashSet<string> { "kick", "clap" }, editor.SoundsPicker.Selected);
    }

    [Fact]
    public void Load_WithNoSounds_StartsAFreshInstrument()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", ["kick"]);

        editor.Load("Instrument", []);

        Assert.Equal("Instrument", editor.NameInput.Value);
        Assert.Empty(editor.SoundsPicker.Selected);
    }

    [Fact]
    public void Load_PreFillsTheSoundsPickerAdjustments()
    {
        var editor = NewEditor(new EditorTestContext());

        editor.Load("Layer", ["kick", "clap"],
            new Dictionary<string, SoundAdjustment> { ["kick"] = new() { Value = -3 } });

        Assert.Equal(-3, editor.SoundsPicker.Adjustments["kick"].Value);
        Assert.False(editor.SoundsPicker.Adjustments.ContainsKey("clap")); // never adjusted -> no entry
    }

    [Fact]
    public void Load_ThenReload_ReplacesThePreviousAdjustments()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", ["kick"], new Dictionary<string, SoundAdjustment> { ["kick"] = new() { Value = -3 } });

        editor.Load("Instrument", []);

        Assert.Empty(editor.SoundsPicker.Adjustments);
    }
}
