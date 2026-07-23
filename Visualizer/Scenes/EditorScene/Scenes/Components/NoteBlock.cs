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

    public TrackSegment? Segment { get; internal set; }
    public Note? Note { get; private set; }

    public void Assign(TrackSegment segment, Note note)
    {
        Segment = segment;
        Note = note;
    }

    /// <summary>
    ///     Press-time selection (both tools, per §2/§3.4), then always starts a group
    ///     drag over whatever ended up selected: pressing an unselected note replaces the
    ///     selection with just it (a group of one — the plain single-note drag); pressing
    ///     a note that's already part of a (possibly multi-note) selection leaves the
    ///     group intact, so the drag moves the whole group together. Ctrl/Shift presses
    ///     under the Select tool only ever append/remove — no drag starts from those.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        if (Note == null || Segment == null) return false;
        _view._state.SelectSegment(Segment);

        if (_view._state.ActiveTool == EditorTool.Select)
        {
            if (_view.FineSnap)
            {
                _view._state.RemoveFromNoteSelection([Note]); // Shift: remove (no-op if absent), no drag
                return true;
            }

            if (_view.WheelZooms)
            {
                _view._state.AddToNoteSelection([Note]); // Ctrl: append (no-op if present), no drag
                return true;
            }
        }

        if (!_view._state.SelectedNotes.Contains(Note)) _view._state.SelectNote(Note); // replace
        _view.BeginNoteDrag(this);
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
        _view.UpdateGroupDrag(x, y);
    }
}
