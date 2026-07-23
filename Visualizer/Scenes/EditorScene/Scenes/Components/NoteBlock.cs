using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

internal class NoteBlock : Panel
{
    private readonly TrackEditorView _view;

    public NoteBlock(UIContext context, TrackEditorView view) : base(context)
    {
        _view = view;
        Width = 0;
        Height = 0;
        Background = new ColoredPlane { Color = TrackEditorView.SoundPalette[0] };
        // Swallow the click so a release on a note never bubbles into the view's
        // place-at-pointer handler; selection already happened on press.
        OnClick = _ => { };
    }

    public TrackSegment? Segment { get; private set; }
    public Note? Note { get; private set; }

    public void Assign(TrackSegment segment, Note note)
    {
        Segment = segment;
        Note = note;
    }

    public override bool HandlePress(float x, float y)
    {
        if (Note == null || Segment == null) return false;
        _view._dragging = this;
        _view._state.BeginGesture();
        _view._state.SelectSegment(Segment);
        _view._state.SelectNote(Note);
        return true;
    }

    /// <summary>Right-click removes the note, same as selecting it and pressing Delete.</summary>
    public override bool HandleRightPress(float x, float y)
    {
        // Ignored mid-drag: deleting the note under the left-button capture would
        // leave the drag mutating a note that is no longer in any segment.
        if (Note == null || Segment == null || _view._dragging == this) return false;
        _view._state.RemoveNote(Segment, Note);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (Note == null || Segment == null) return;
        var (segment, step) = _view.StepAt(x, true);
        if (segment == null) return;

        var value = _view.ValueAt(y);
        var moved = segment != Segment || step != Note.Step || value != Note.Value;
        _view._state.MoveNote(Segment, segment, Note, step, value);
        Segment = segment;
        _view.InvalidateLayout();
        // Re-preview only on an actual cell change, replacing the old preview.
        if (moved) _view.OnPreviewNote?.Invoke(Note.Instrument, Note.Value);
    }
}
