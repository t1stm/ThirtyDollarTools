using EditorScene.Scenes.Dialogs;
using EditorScene.State;
using OpenTK.Mathematics;
using Serilog;
using Shared.Atlases;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

public class InstrumentEditorTests
{
    private static InstrumentEditor NewEditor(EditorTestContext ctx)
    {
        var atlases = new AtlasStore((AssetProvider)ctx.AssetProvider, new LoggerConfiguration().CreateLogger());
        var editor = new InstrumentEditor(ctx, atlases);
        editor.EnsureSounds(((string[])["kick", "clap", "noteblock_harp", "noteblock_bell"]).Select(id =>
            new Sound { Id = id }));
        return editor;
    }

    /// <summary>Styled and laid out in a minimum-size window, as the modal shows it.</summary>
    private static (InstrumentEditor Editor, Panel Root) Shown(EditorTestContext ctx)
    {
        var editor = EditorTestContext.Styled(NewEditor(ctx));
        var root = EditorTestContext.Styled(new Panel(ctx) { Width = 1080, Height = 720 });
        root.AddChild(editor);
        root.Layout();
        return (editor, root);
    }

    private static Instrument Layered(params string[] sounds)
    {
        var instrument = new Instrument { Name = "Layer" };
        foreach (var sound in sounds) instrument.AddSound(sound);
        return instrument;
    }

    private static readonly Vector2 Up = new(0, 1);

    [Fact]
    public void Load_CopiesEveryField_AndCommitWritesThemBack()
    {
        var editor = NewEditor(new EditorTestContext());
        var source = Layered("kick", "clap");
        source.Value = 2;
        source.Volume = 80;
        source.Pan = -10;
        source.Offset = 0.03;
        source.Sounds[0].Value = -3;
        source.Sounds[1].Offset = 0.02;

        editor.Load("Layer", source);

        Assert.Equal("Layer", editor.NameInput.Value);
        Assert.Equal((2, 80, -10f, 0.03), (editor.Working.Value, editor.Working.Volume, editor.Working.Pan,
            editor.Working.Offset));
        Assert.Equal(["kick", "clap"], editor.Working.Sounds.Select(sound => sound.Sound));
        Assert.Equal(-3, editor.Working.Sounds[0].Value);
        Assert.Equal(0.02, editor.Working.Sounds[1].Offset);

        // Copies: tuning the working copy leaves the loaded instrument alone until Done.
        editor.Working.Sounds[0].Value = -12;
        editor.Working.Pan = 30;
        Assert.Equal(-3, source.Sounds[0].Value);
        Assert.Equal(-10, source.Pan);

        // Done writes it back the way InstrumentWorkflow.Commit does.
        var state = new EditorState();
        state.SetInstrumentSounds(source, editor.Working.Sounds);
        state.SetInstrumentModifiers(source, editor.Working.Value, editor.Working.Volume, editor.Working.Pan,
            editor.Working.Offset);
        Assert.Equal(-12, source.Sounds[0].Value);
        Assert.Equal(0.02, source.Sounds[1].Offset);
        Assert.Equal(30, source.Pan);
        Assert.Equal(80, source.Volume);
        Assert.NotSame(editor.Working.Sounds[0], source.Sounds[0]);
    }

    [Fact]
    public void Load_WithNoInstrument_StartsBlank()
    {
        var editor = NewEditor(new EditorTestContext());
        var loaded = Layered("kick");
        loaded.Value = 5;
        editor.Load("Layer", loaded);

        editor.Load("Instrument", null);

        Assert.Equal("Instrument", editor.NameInput.Value);
        Assert.Empty(editor.Working.Sounds);
        Assert.Equal(0, editor.Working.Value);
        Assert.Null(editor.Selected);
        Assert.Empty(editor.Rows);
    }

    [Fact]
    public void Load_SelectsTheFirstSound_AndBuildsARowPerSound()
    {
        var editor = NewEditor(new EditorTestContext());

        editor.Load("Layer", Layered("kick", "clap", "kick"));

        Assert.Equal(3, editor.Rows.Count);
        Assert.Same(editor.Working.Sounds[0], editor.Selected);
        Assert.Equal(["kick", "clap", "kick"], editor.Rows.Select(row => row.Instance.Sound));
    }

    [Fact]
    public void Duplicate_InsertsACopyAfterItsRow_AndSelectsIt()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick", "clap"));
        editor.Working.Sounds[0].Value = -12;

        editor.Rows[0].DuplicateButton.OnClick!(editor.Rows[0].DuplicateButton);

        Assert.Equal(["kick", "kick", "clap"], editor.Working.Sounds.Select(sound => sound.Sound));
        Assert.Equal(-12, editor.Working.Sounds[1].Value);
        Assert.NotSame(editor.Working.Sounds[0], editor.Working.Sounds[1]);
        Assert.Same(editor.Working.Sounds[1], editor.Selected);
        Assert.Equal(3, editor.Rows.Count);
    }

    [Fact]
    public void Remove_MovesASelectionItTookToTheSoundInItsPlace()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick", "clap", "noteblock_harp"));
        var clap = editor.Working.Sounds[1];
        var harp = editor.Working.Sounds[2];
        editor.Select(clap);

        editor.Remove(clap);
        Assert.Same(harp, editor.Selected);

        editor.Remove(harp); // the last one: the selection steps back
        Assert.Same(editor.Working.Sounds[0], editor.Selected);

        editor.Remove(editor.Working.Sounds[0]);
        Assert.Null(editor.Selected);
    }

    [Fact]
    public void Remove_KeepsAnotherRowsSelection()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick", "clap", "noteblock_harp"));
        var harp = editor.Working.Sounds[2];
        editor.Select(harp);

        editor.Remove(editor.Working.Sounds[0]);

        Assert.Same(harp, editor.Selected);
    }

    [Fact]
    public void Filter_HidesNonMatchingAvailableSounds()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick"));

        editor.FilterInput.Value = "noteblock";

        Assert.Equal(["noteblock_harp", "noteblock_bell"], editor.AvailablePicker.AvailableSounds);
    }

    [Fact]
    public void AllSoundsHeading_FollowsTheCount()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick", "clap"));
        Assert.Equal("All sounds - 2", editor.AllHeaderText);

        editor.Duplicate(editor.Working.Sounds[0]);
        Assert.Equal("All sounds - 3", editor.AllHeaderText);

        editor.Load("Instrument", null);
        Assert.Equal("All sounds - 0", editor.AllHeaderText);
    }

    [Fact]
    public void TypingInAField_TunesTheWorkingCopy_AndTheRowReadsIt()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick", "clap"));
        editor.Select(editor.Working.Sounds[1]);

        editor.Field("Sound.Pan")!.Value = -30;
        editor.Field("All.Volume")!.Value = 50;

        Assert.Equal(-30, editor.Working.Sounds[1].Pan);
        Assert.Equal(50, editor.Working.Volume);
        Assert.Equal(["—", "—", "<30", "—"], editor.Rows[1].CellTexts);
    }

    // ---------------------------------------------------------------- scroll gestures

    [Theory]
    [InlineData(true, true, false, "value", 0.1)]
    [InlineData(true, false, false, "volume", 105)]
    [InlineData(false, true, false, "pan", 10)]
    [InlineData(false, false, true, "offset", 0.01)]
    [InlineData(true, true, true, "offset", 0.01)] // Alt wins
    public void ScrollingARowsReadOuts_TunesThatSound_SelectsAndPreviewsIt(bool ctrl, bool shift, bool alt,
        string field, double expected)
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick", "clap"));
        InstrumentSound? previewed = null;
        editor.OnPreviewSound = sound => previewed = sound;
        var clap = editor.Rows[1];

        editor.SetModifiers(ctrl, shift, alt);
        var handled = clap.NumberZone.HandleScroll(Up);

        Assert.True(handled);
        var sound = clap.Instance;
        Assert.Equal(expected, field switch
        {
            "value" => sound.Value,
            "volume" => sound.Volume,
            "pan" => sound.Pan,
            _ => sound.Offset
        }, 6);
        Assert.Same(sound, editor.Selected);
        Assert.Same(sound, previewed);
        Assert.Equal(0, editor.Working.Value); // the instrument-wide fields are untouched
    }

    [Fact]
    public void ScrollingARowsName_PassesTheWheelOnToTheList()
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick", "clap", "noteblock_harp", "noteblock_bell", "kick", "clap", "kick"));
        root.Layout();
        var row = editor.Rows[0];

        Assert.False(row.Select.HandleScroll(-Up));
        Assert.True(editor.SoundsList.MaxScroll > 0);

        var name = row.Select.Children[1].Computed;
        ctx.UpdatePointer(root, name.AbsoluteX + 4, name.AbsoluteY + 4, false, false, false, -Up);

        Assert.True(editor.SoundsList.ScrollY > 0);
        Assert.Equal(0, editor.Working.Sounds[0].Value);
    }

    [Fact]
    public void ScrollingTheAllSoundsCard_TunesOnlyTheInstrumentWideFields()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick"));

        editor.SetModifiers(true, false, false);
        Assert.True(editor.AllCard.HandleScroll(-Up));

        Assert.Equal(95, editor.Working.Volume);
        Assert.Equal(100, editor.Working.Sounds[0].Volume);
    }

    /// <summary>One wheel notch with the pointer 4 px inside <paramref name="element" />'s box.</summary>
    private static void ScrollOver(EditorTestContext ctx, Panel root, Sundex.Components.Abstractions.UIElement element,
        Vector2 delta)
    {
        var box = element.Computed;
        ctx.UpdatePointer(root, box.AbsoluteX + 4, box.AbsoluteY + 4, false, false, false, delta);
    }

    [Fact]
    public void APlainNotchOverAField_StepsThatField()
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick"));
        root.Layout();

        ScrollOver(ctx, root, editor.Field("Sound.Pan")!, Up);
        ScrollOver(ctx, root, editor.Field("All.Volume")!, -Up);

        Assert.Equal(10, editor.Working.Sounds[0].Pan);
        Assert.Equal(95, editor.Working.Volume);
        Assert.Equal(0, editor.Working.Sounds[0].Value);
        // The field under the pointer is the one lit.
        Assert.Contains("instrument-field-target", editor.Field("All.Volume")!.Classes);
    }

    [Theory]
    [InlineData(true, false, false, "Volume", 105)]
    [InlineData(false, true, false, "Pan", 10)]
    [InlineData(false, false, true, "Offset", 0.01)]
    [InlineData(true, true, false, "Value", 0.1)]
    public void AModifierOverAField_TunesItsOwnField(bool ctrl, bool shift, bool alt, string field, double expected)
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick"));
        root.Layout();
        var sound = editor.Working.Sounds[0];

        // Over the Value field, or the Pan one for the value gestures: never the field the
        // modifier picks, so it has to be the modifier that reached it.
        var under = field == "Value" ? "Pan" : "Value";
        editor.SetModifiers(ctrl, shift, alt);
        ScrollOver(ctx, root, editor.Field($"Sound.{under}")!, Up);

        double Read(string name) => name switch
        {
            "Value" => sound.Value,
            "Volume" => sound.Volume,
            "Pan" => sound.Pan,
            _ => sound.Offset
        };

        Assert.Equal(expected, Read(field), 6);
        Assert.Equal(0, Read(under));
        Assert.Contains("instrument-field-target", editor.Field($"Sound.{field}")!.Classes);
        Assert.DoesNotContain("instrument-field-target", editor.Field($"Sound.{under}")!.Classes);

        // The card does the same for the instrument-wide fields.
        ScrollOver(ctx, root, editor.Field($"All.{under}")!, Up);
        Assert.Equal(field switch
        {
            "Value" => editor.Working.Value,
            "Volume" => editor.Working.Volume,
            "Pan" => editor.Working.Pan,
            _ => editor.Working.Offset
        }, expected, 6);
    }

    [Fact]
    public void Volume_IsANumber_100ByDefault()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick"));

        Assert.Equal("100", editor.Field("Sound.Volume")!.Text);
        Assert.Equal("100", editor.Field("All.Volume")!.Text);
        Assert.False(editor.Field("Sound.Volume")!.AllowNull);
        // At its default it reads as a dash in the row, like every other field.
        Assert.Equal("\u2014", editor.Rows[0].CellTexts.ElementAt(1));
        Assert.Equal("100%", editor.ResultTexts[1]);

        editor.Field("Sound.Volume")!.Value = 50;
        Assert.Equal("50%", editor.Rows[0].CellTexts.ElementAt(1));
        Assert.Equal("50%", editor.ResultTexts[1]);
    }

    [Fact]
    public void Offset_NeverGoesBelowZero()
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick"));
        root.Layout();

        editor.Field("Sound.Offset")!.Value = -1;
        Assert.Equal(0, editor.Working.Sounds[0].Offset);

        editor.SetModifiers(false, false, true);
        ScrollOver(ctx, root, editor.Rows[0].NumberZone, -Up);
        ScrollOver(ctx, root, editor.AllCard, -Up);
        Assert.Equal(0, editor.Working.Sounds[0].Offset);
        Assert.Equal(0, editor.Working.Offset);
    }

    [Fact]
    public void ANotchBetweenFields_TunesNothing_UnlessAModifierIsHeld()
    {
        var editor = NewEditor(new EditorTestContext());
        editor.Load("Layer", Layered("kick"));
        var sound = editor.Working.Sounds[0];

        // Plain, off any field: nothing is the target, not the value.
        Assert.True(editor.SoundFields.HandleScroll(Up));
        Assert.True(editor.AllCard.HandleScroll(Up));
        Assert.Equal(0, sound.Value);
        Assert.Equal(0, editor.Working.Value);

        editor.SetModifiers(true, false, false);
        editor.SoundFields.HandleScroll(Up);
        Assert.Equal(105, sound.Volume);
    }

    [Theory]
    [InlineData(0, "Value", 1)]
    [InlineData(1, "Volume", 105)]
    [InlineData(2, "Pan", 10)]
    [InlineData(3, "Offset", 0.01)]
    public void APlainNotchOverARowsReadOut_TunesThatReadOut(int column, string field, double expected)
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick", "clap"));
        root.Layout();
        var clap = editor.Rows[1];

        ScrollOver(ctx, root, clap.NumberZone.Children[column], Up);

        var sound = clap.Instance;
        Assert.Equal(expected, field switch
        {
            "Value" => sound.Value,
            "Volume" => sound.Volume,
            "Pan" => sound.Pan,
            _ => sound.Offset
        }, 6);
        Assert.Same(sound, editor.Selected);
        // Its cell, its column's head and the selected sound's field light up; nothing else.
        Assert.Contains("sound-row-cell-target", clap.NumberZone.Children[column].Classes);
        Assert.Equal(Enumerable.Range(0, 4).Select(i => i == column),
            editor.ColumnHeadChips.Select(chip => chip.Classes.Contains("scroll-chip-lit")));
        Assert.Contains("instrument-field-target", editor.Field($"Sound.{field}")!.Classes);
    }

    [Fact]
    public void ARowsReadOuts_LightNothing_UntilOneIsHovered()
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick"));
        root.Layout();
        var row = editor.Rows[0];

        // At rest no chip claims the plain wheel.
        Assert.DoesNotContain(editor.ColumnHeadChips, chip => chip.Classes.Contains("scroll-chip-lit"));

        // The band's own padding, left of the first read-out: the band lights, no cell does,
        // and a plain notch there changes nothing (and doesn't scroll the list either).
        var zone = row.NumberZone.Computed;
        ctx.UpdatePointer(root, zone.AbsoluteX + 2, zone.AbsoluteY + 2, false, false, false, Up);
        Assert.Contains("sound-row-zone-hovered", row.NumberZone.Classes);
        Assert.DoesNotContain(row.NumberZone.Children, cell => cell.Classes.Contains("sound-row-cell-target"));
        Assert.Equal(0, row.Instance.Value);
    }

    [Fact]
    public void HeldKeys_LightTheirChip_AndHoveringAZoneLightsItsCellAndField()
    {
        var ctx = new EditorTestContext();
        var (editor, root) = Shown(ctx);
        editor.Load("Layer", Layered("kick", "clap"));
        root.Layout();

        editor.SetModifiers(false, false, true);
        Assert.Equal([false, false, false, true], editor.ColumnHeadChips.Select(chip => chip.Classes.Contains("scroll-chip-lit")));

        var zone = editor.Rows[0].NumberZone.Computed;
        ctx.UpdatePointer(root, zone.AbsoluteX + 4, zone.AbsoluteY + 4, false, false, false, Vector2.Zero);
        Assert.Contains("instrument-field-target", editor.Field("Sound.Offset")!.Classes);
        Assert.DoesNotContain("instrument-field-target", editor.Field("All.Offset")!.Classes);

        editor.SetModifiers(true, true, false);
        Assert.Contains("scroll-chip-lit", editor.FineChip.Classes);
        Assert.Contains("instrument-field-target", editor.Field("Sound.Value")!.Classes);
    }
}
