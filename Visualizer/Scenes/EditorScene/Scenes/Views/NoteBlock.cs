using EditorScene.State;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

/// <summary>
///     One note's hit box. It has no background of its own: the view writes the fill into
///     <see cref="BatchSlot" /> of its line batch, so the whole pool costs no draw calls.
///     The element exists only to receive clicks, right-clicks and drags.
/// </summary>
internal class NoteBlock : Panel
{
    private readonly TrackEditorView _view;

    public NoteBlock(UIContext context, TrackEditorView view) : base(context)
    {
        _view = view;
        Width = 0;
        Height = 0;
        Cursor = CursorType.Pointer;
        // Swallow the click so a release on a note never bubbles into the view's
        // place-at-pointer handler; selection already happened on press.
        OnClick = _ => { };
    }

    /// <summary>This block's fixed slot in the view's line batch - its pool position.</summary>
    public required int BatchSlot { get; init; }

    public TrackSegment? Segment { get; internal set; }
    public Note? Note { get; private set; }

    public void Assign(TrackSegment segment, Note note)
    {
        Segment = segment;
        Note = note;
    }

    /// <summary>
    ///     Selects on press, under both tools. Select only ever selects - Ctrl appends,
    ///     Shift removes, a plain press replaces the selection - and never starts a drag.
    ///     Draw's plain press replaces the selection the same way, then starts a group drag
    ///     over whatever ended up selected: pressing an unselected note drags that note
    ///     alone, pressing one already in the selection moves the whole group together.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        if (Note == null || Segment == null) return false;
        _view._state.SelectSegment(Segment);

        if (_view._state.ActiveTool == EditorTool.Select)
        {
            if (_view.FineSnap)
                _view._state.RemoveFromNoteSelection([Note]);
            else if (_view.WheelZooms)
                _view._state.AddToNoteSelection([Note]);
            else if (!_view._state.SelectedNotes.Contains(Note))
                _view._state.SelectNote(Note);

            return true;
        }

        if (!_view._state.SelectedNotes.Contains(Note)) _view._state.SelectNote(Note);
        _view.BeginNoteDrag(this, y);
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