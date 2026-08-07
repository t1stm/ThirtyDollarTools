using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

/// <summary>
///     One segment's strip: a hit box only, like <see cref="NoteBlock" /> - its fill is
///     written into <see cref="BatchSlot" /> of the view's line batch.
/// </summary>
internal class StripBlock : Panel
{
    public StripBlock(UIContext context, TrackEditorView view) : base(context)
    {
        Width = 0;
        Height = 0;
        Cursor = CursorType.Pointer;
        OnClick = _ =>
        {
            if (Segment != null) view._state.SelectSegment(Segment);
        };
    }

    /// <summary>This block's fixed slot in the view's line batch - its pool position.</summary>
    public required int BatchSlot { get; init; }

    public TrackSegment? Segment { get; set; }
}
