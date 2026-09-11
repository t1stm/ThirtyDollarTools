using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Layout;
using EditorScene.Scenes.Views;
using EditorScene.State;
using OpenTK.Mathematics;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

/// <summary>
///     The editor side of a wave reference track: how it is added, what the track list and the
///     inspector say about it, and the two editors it deliberately does not open.
/// </summary>
public class WaveTrackUiTests
{
    private static WaveTrack AddWave(EditorState state, string path = "/music/reference.wav", double seconds = 30)
    {
        return state.AddWaveTrack(path, seconds);
    }

    [Fact]
    public void AddWaveTrack_NamesItAfterTheFile_AndPlacesIt()
    {
        var state = new EditorState();
        var track = AddWave(state);

        Assert.Equal("reference", track.Name);
        Assert.Equal(30, track.DurationSeconds);
        var placement = Assert.Single(state.Project.Placements);
        Assert.Same(track, placement.Track);
        Assert.Equal(0, placement.StartQuarterNotes);
    }

    [Fact]
    public void AddWaveTrack_LandsOnTheFirstEmptyChannel()
    {
        var state = new EditorState();
        state.PlaceTrack(state.AddTrack(), 0, 0);
        state.PlaceTrack(state.AddTrack(), 1, 4);

        AddWave(state);

        Assert.Equal(2, state.Project.Placements[^1].Channel);
    }

    [Fact]
    public void AddWaveTrack_UndoesAsOneStep()
    {
        var state = new EditorState();
        AddWave(state);

        state.Undo();

        Assert.Empty(state.Project.Tracks);
        Assert.Empty(state.Project.Placements);

        state.Redo();
        Assert.Single(state.Project.Tracks);
        Assert.Single(state.Project.Placements);
    }

    [Fact]
    public void SetWaveFile_SwapsTheFileAndItsLength_Undoably()
    {
        var state = new EditorState();
        var track = AddWave(state);

        state.SetWaveFile(track, "/music/other.wav", 12.5);
        Assert.Equal("/music/other.wav", track.Path);
        Assert.Equal(12.5, track.DurationSeconds);

        state.Undo();
        Assert.Equal("/music/reference.wav", track.Path);
        Assert.Equal(30, track.DurationSeconds);
        Assert.Single(state.Project.Placements); // the clip stayed put through both
    }

    [Fact]
    public void AWaveTrack_NeverOpensAnEditor()
    {
        var state = new EditorState();
        var track = AddWave(state);

        state.OpenTrack(track);

        Assert.Null(state.OpenedTrack);
        Assert.Null(state.OpenedFaithfulTrack);
    }

    [Fact]
    public void DoubleClickingItsRow_LeavesTheArrangementOpen()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var row = EditorTestContext.Styled(new EditorTrack(ctx, AddWave(state), state));
        row.Width = 200;
        row.Layout();

        for (var i = 0; i < 2; i++)
        {
            ctx.UpdatePointer(row, 195, 30, true, true, false, Vector2.Zero);
            ctx.UpdatePointer(row, 195, 30, false, false, true, Vector2.Zero);
        }

        Assert.Null(state.OpenedTrack);
    }

    [Fact]
    public void TheRowsBlip_CarriesAW()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var row = EditorTestContext.Styled(new EditorTrack(ctx, AddWave(state), state, new Vector4(1, 0, 0, 1)));

        var letter = Assert.IsAssignableFrom<Label>(Assert.Single(row.DragHandle!.Children));
        Assert.Equal("W", letter.Value);
    }

    [Fact]
    public void TheInspector_ShowsTheFileAndEditsItsVolume()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var inspector = ctx.NewInspector(state);
        inspector.Element.Width = 260;
        inspector.Element.Height = 600;
        state.OnSelectionChanged += _ => inspector.Rebuild();

        var track = AddWave(state);
        state.SelectTrack(track);

        // The file is gone from disk in a test, which is exactly the case the row has to say
        // something about rather than hide.
        Assert.Contains("reference.wav", ((Label)inspector.Field("Wave track.File")!).Value);
        Assert.Contains("missing", ((Label)inspector.Field("Wave track.File")!).Value);
        Assert.Equal("30 s", ((Label)inspector.Field("Wave track.Length")!).Value);

        ((NumericInput)inspector.Field("Wave track.Volume")!).Value = 60;
        Assert.Equal(60, track.Volume);

        // A stem sits well below the song's own level, so the gain has to reach far past the
        // 200% a note tops out at - see the comment on the row.
        ((NumericInput)inspector.Field("Wave track.Volume")!).Value = 600;
        Assert.Equal(600, track.Volume);

        // No tempo or automation rows: a reference plays at its own rate, whatever the project does.
        Assert.Null(inspector.Field("Wave track.Project tempo"));
        Assert.Null(inspector.Field("Wave track.BPM"));
    }

    [Fact]
    public void TheClip_DrawsThePeakEnvelope_OnceTheFileHasDecoded()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var view = EditorTestContext.Styled(new ArrangementView(ctx, state) { Width = 800, Height = 400 });
        state.OnProjectChanged += view.Refresh;

        float[]? peaks = null;
        view.WavePeaks = _ => peaks;
        AddWave(state, seconds: 60); // 120 quarters at 120 BPM - a clip wider than the view
        view.Layout();

        Assert.Equal(0, view.WaveBarsDrawn); // still decoding: the clip draws, the envelope doesn't

        peaks = [.. Enumerable.Range(0, 2048).Select(i => i % 2 == 0 ? 1f : 0f)];
        view.InvalidateLayout(); // what EditorPlayback.OnWaveDecoded fires in the editor
        view.Layout();

        Assert.InRange(view.WaveBarsDrawn, 1, 2048);
        // Bars stop at the view's right edge rather than running the whole 60 s clip.
        Assert.True(view.WaveBarsDrawn <= 800 / 3 + 1, $"{view.WaveBarsDrawn} bars for an 800 px view");
    }

    [Fact]
    public void TheEnvelopeGoesAway_WithItsClip()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var view = EditorTestContext.Styled(new ArrangementView(ctx, state) { Width = 800, Height = 400 });
        state.OnProjectChanged += view.Refresh;
        view.WavePeaks = _ => [.. Enumerable.Repeat(1f, 2048)];

        var track = AddWave(state);
        view.Layout();
        Assert.True(view.WaveBarsDrawn > 0);

        state.RemoveTrack(track);
        view.Layout();

        Assert.Equal(0, view.WaveBarsDrawn);
    }

    [Fact]
    public void RemovingAnEarlierClip_LeavesNoStaleBars()
    {
        // The envelope's slots start where the clip range ends, so losing a clip slides the
        // whole range down - and the bars the old, higher range had painted are still there
        // unless they are released against where they actually were.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var view = EditorTestContext.Styled(new ArrangementView(ctx, state) { Width = 800, Height = 400 });
        state.OnProjectChanged += view.Refresh;
        view.WavePeaks = _ => [.. Enumerable.Repeat(1f, 2048)];

        var filler = state.AddTrack();
        state.PlaceTrack(filler, 5, 0);
        AddWave(state);
        view.Layout();
        var drawn = view.WaveBarsDrawn;
        var lastSlot = view.WaveBarSlot(drawn - 1);

        state.RemoveTrack(filler);
        view.Layout();

        Assert.Equal(drawn, view.WaveBarsDrawn); // same clip, same envelope
        Assert.True(view.WaveBarSlot(drawn - 1) < lastSlot, "the range did not move");
        Assert.Equal(default, view.BatchColorAt(lastSlot));
    }

    [Fact]
    public void ADropOfWaveFiles_TakesAllOfThem_WhateverTheCase()
    {
        // Unlike a sequence drop (one dialog, so one file), a reference needs no dialog -
        // dropping a folder of stems in one go has to add all of them.
        string[] dropped =
        [
            "/music/MOON/bass.wav",
            "/music/MOON/drums.WAV",
            "/music/MOON/notes.txt",
            "/music/MOON/other.wav"
        ];

        Assert.Equal(["/music/MOON/bass.wav", "/music/MOON/drums.WAV", "/music/MOON/other.wav"],
            Editor.WaveFiles(dropped));
    }

    [Fact]
    public void ADropWithNoWaveFiles_TakesNone()
    {
        Assert.Empty(Editor.WaveFiles(["/music/song.tdwproj", "/music/cover.tdw", "/music/stems"]));
    }

    [Fact]
    public void TheExportDialog_CarriesTheNotExportedCaption()
    {
        var dialog = new ExportDialog(new EditorTestContext());

        Assert.Contains("not exported", dialog.WaveNote.Value);
        Assert.Contains(dialog.WaveNote, dialog.Element.Children); // removable, so it must be a child
    }
}
