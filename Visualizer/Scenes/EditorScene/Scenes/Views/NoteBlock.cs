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

    /// <summary>
    ///     How wide the grab zone on each border is: 6 px, or a third of the block when it is
    ///     narrower than that, so a one-step note at low zoom keeps a middle to press.
    /// </summary>
    public float EdgeZone => Math.Min(6f, (float)Computed.Width / 3f);

    /// <summary>
    ///     Which border the given absolute x is on: -1 left, +1 right, 0 for the body. Cuts
    ///     have no length to drag, so they are all body.
    /// </summary>
    public int EdgeAt(float absoluteX)
    {
        if (Note is null or { IsCut: true } || Computed.Width <= 0) return 0;

        var local = absoluteX - (float)Computed.AbsoluteX;
        if (local <= EdgeZone) return -1;
        if (local >= (float)Computed.Width - EdgeZone) return 1;
        return 0;
    }

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
    ///     Reaching for a note also picks its instrument (see
    ///     <see cref="TrackEditorView.PickInstrument" />) - every press here except the
    ///     Shift one, which is putting a note down rather than reaching for it.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        if (Note == null || Segment == null) return false;
        _view._state.SelectSegment(Segment);

        // A border press resizes under both tools: the marquee only ever starts on empty
        // grid, so nothing of the Select tool conflicts with it. Selection first, the same
        // way a move does it - a border of an already-selected note resizes the whole group.
        if (EdgeAt(x) is var edge and not 0)
        {
            if (!_view._state.SelectedNotes.Contains(Note)) _view._state.SelectNote(Note);
            _view.PickInstrument(Note);
            _view.BeginNoteResize(this, edge, x);
            return true;
        }

        if (_view._state.ActiveTool == EditorTool.Select)
        {
            if (_view.FineSnap)
            {
                _view._state.RemoveFromNoteSelection([Note]);
                return true;
            }

            if (_view.WheelZooms) _view._state.AddToNoteSelection([Note]);
            else if (!_view._state.SelectedNotes.Contains(Note)) _view._state.SelectNote(Note);

            _view.PickInstrument(Note);
            return true;
        }

        if (!_view._state.SelectedNotes.Contains(Note)) _view._state.SelectNote(Note);
        _view.PickInstrument(Note);
        _view.BeginNoteDrag(this, x, y);
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
        _view.UpdateDrag(x, y);
    }

    /// <summary>
    ///     The borders are only shown by the cursor - nothing is drawn on the block at rest,
    ///     so a dense chart stays readable (the view lightens the hovered border band).
    /// </summary>
    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        if (IsHovered && EdgeAt(uiContext.PointerX) != 0) uiContext.RequestCursor(CursorType.ResizeX);
    }
}