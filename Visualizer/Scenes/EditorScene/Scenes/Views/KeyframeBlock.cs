using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

/// <summary>
///     One automation keyframe's grab box. Like <see cref="NoteBlock" /> it carries no fill
///     of its own - the marker is drawn by <see cref="AutomationPath" /> - but it is several
///     pixels wider than the 1 px mark so a marker stays catchable at any zoom. The pool is
///     added to the view after the note pool, so a marker sitting on a note body wins the
///     press over the body it is drawn on.
/// </summary>
internal sealed class KeyframeBlock : Panel
{
    /// <summary>Half the grab box, in pixels, around the marker's own point.</summary>
    public const float GrabRadius = 5f;

    private readonly TrackEditorView _view;

    public KeyframeBlock(UIContext context, TrackEditorView view) : base(context)
    {
        _view = view;
        Width = 0;
        Height = 0;
        Cursor = CursorType.Pointer;
        OnClick = _ => { }; // never bubble into the view's place-at-pointer handler
    }

    public Note? Note { get; private set; }
    public TrackSegment? Segment { get; private set; }

    /// <summary>
    ///     Which keyframe of <see cref="Note" />'s automation this marker is. Not "Index":
    ///     that is the element's depth layer, which decides paint order and hit-test ties.
    /// </summary>
    public int KeyframeIndex { get; private set; }

    public void Assign(Note note, TrackSegment segment, int index)
    {
        Note = note;
        Segment = segment;
        KeyframeIndex = index;
    }

    public override bool HandlePress(float x, float y)
    {
        if (Note?.Automation is null || Segment == null) return false;
        _view._state.SelectSegment(Segment);
        _view.BeginKeyframeDrag(this);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (Note?.Automation is null) return;
        _view.UpdateKeyframeDrag(x, y);
    }
}
