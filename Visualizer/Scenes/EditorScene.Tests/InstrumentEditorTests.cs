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

    private static InstrumentSound Sound(string sound, double value = 0)
    {
        return new InstrumentSound { Sound = sound, Value = value };
    }

    [Fact]
    public void Load_PreFillsTheNameAndTheSoundSelection()
    {
        var editor = NewEditor(new EditorTestContext());

        editor.Load("Layer", [Sound("kick"), Sound("clap")]);

        Assert.Equal("Layer", editor.NameInput.Value);
        Assert.Equal(new HashSet<string> { "kick", "clap" }, editor.SoundsPicker.Selected);
    }

    [Fact]
    public void Load_WithNoSounds_StartsAFreshInstrument()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", [Sound("kick")]);

        editor.Load("Instrument", []);

        Assert.Equal("Instrument", editor.NameInput.Value);
        Assert.Empty(editor.SoundsPicker.Instances);
    }

    [Fact]
    public void Load_PreFillsTheSoundsPickerAdjustments()
    {
        var editor = NewEditor(new EditorTestContext());

        editor.Load("Layer", [Sound("kick", -3), Sound("clap")]);

        Assert.Equal(-3, editor.SoundsPicker.Instances[0].Value);
        Assert.Equal(0, editor.SoundsPicker.Instances[1].Value);
    }

    [Fact]
    public void Load_CopiesTheInstances_SoEditingThemCantReachTheInstrument()
    {
        var editor = NewEditor(new EditorTestContext());
        var loaded = Sound("kick", -3);

        editor.Load("Layer", [loaded]);
        editor.SoundsPicker.Instances[0].Value = -12;

        Assert.Equal(-3, loaded.Value);
    }

    [Fact]
    public void Load_ThenReload_ReplacesThePreviousInstances()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", [Sound("kick", -3)]);

        editor.Load("Instrument", []);

        Assert.Empty(editor.SoundsPicker.Instances);
    }
}
