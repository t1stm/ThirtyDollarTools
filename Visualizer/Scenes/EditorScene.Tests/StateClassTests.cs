using EditorScene.State;
using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Panels;
using EditorScene.Scenes.Layout;
using EditorScene.Scenes.Views;

namespace EditorScene.Tests;

/// <summary>
///     The editor's runtime states, which are style classes added and removed as the
///     state changes rather than colors assigned to a renderable. Each of these asserts
///     the state reads differently from the resting look and goes back when it clears -
///     the values themselves are the sheet's business, so nothing here hard-codes one.
///     <see cref="StyleSelectorTests.EveryClassAddedAtRuntime_IsDefinedByASheet" /> covers
///     the class names these go through.
/// </summary>
public class StateClassTests
{
    private static Vector4 FillOf(Panel panel)
    {
        return ((ColoredPlane)panel.Background!).Color;
    }

    [Fact]
    public void SelectingATrackRow_SwapsItsFill_AndDeselectingRestoresIt()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var row = EditorTestContext.Styled(new EditorTrack(ctx, state.AddTrack(), state));

        var resting = FillOf(row);
        // An unselected row has no fill of its own: transparent, and skipped by the render
        // pass rather than drawn as an invisible quad.
        Assert.Equal(0, resting.W);
        Assert.False(row.Background!.IsVisible);

        row.SetSelected(true);
        Assert.NotEqual(resting, FillOf(row));
        Assert.True(row.Background!.IsVisible);

        row.SetSelected(false);
        Assert.Equal(resting, FillOf(row));
        Assert.False(row.Background!.IsVisible);
    }

    [Fact]
    public void SelectingAClip_SwapsItsFill_AndDeselectingRestoresIt()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var view = EditorTestContext.Styled(new ArrangementView(ctx, state) { Width = 800, Height = 400 });
        state.OnPlacementSelectionChanged += _ => view.RefreshSelection();
        var placement = state.PlaceTrack(state.AddTrack(), 0, 0);
        view.Refresh();
        view.Layout();

        // A clip's fill is a batch slot, not a background on the element, so its selected
        // shade is a setting the layout picks - read it back the same way.
        var block = view.Blocks[0];
        var resting = view.FillOf(block);
        state.SetPlacementSelection([placement]);
        view.Layout();
        Assert.NotEqual(resting, view.FillOf(block));

        state.SetPlacementSelection([]);
        view.Layout();
        Assert.Equal(resting, view.FillOf(block));
    }

    [Fact]
    public void MutingAndSoloingALane_TintTheToggleLabels_Differently()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var arrangement = new ArrangementView(ctx, state) { Width = 800, Height = 400 };
        var header = EditorTestContext.Styled(new LaneHeader(ctx, state, arrangement));
        var (mute, solo) = header.Rows[0];

        var resting = mute.Label.Color;
        Assert.Equal(resting, solo.Label.Color);

        state.ToggleMute(0);
        state.ToggleSolo(0);
        header.RefreshChannels();

        // Muted is the danger red, soloed the yellow: both lit, and not the same shade.
        Assert.NotEqual(resting, mute.Label.Color);
        Assert.NotEqual(resting, solo.Label.Color);
        Assert.NotEqual(mute.Label.Color, solo.Label.Color);

        state.ToggleMute(0);
        state.ToggleSolo(0);
        header.RefreshChannels();
        Assert.Equal(resting, mute.Label.Color);
        Assert.Equal(resting, solo.Label.Color);
    }

    /// <summary>
    ///     The one setting that isn't a single color: the note palette is an array in the
    ///     sheet, so a note block's fill proves the whole array reached the view.
    /// </summary>
    [Fact]
    public void NoteBlocks_TakeTheirFill_FromTheSheetsSoundPalette()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        var view = EditorTestContext.Styled(new TrackEditorView(ctx, state)
            { Width = 800, Height = 414, PixelsPerStep = 16f, RowHeight = 8f });

        Assert.NotEmpty(view.SoundPalette);

        state.AddNote(track.Segments[0], 0, state.AddInstrument("boom"), 0);
        view.InvalidateLayout();
        view.Layout();

        // A note's fill lives in the view's batch slot, not on the (background-less) block.
        var painted = view.NoteBlocks.First(block => block.Note is not null);
        Assert.Contains(view.FillOf(painted), view.SoundPalette);
    }
}
