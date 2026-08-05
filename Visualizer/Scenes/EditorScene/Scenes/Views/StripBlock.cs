using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

/// <summary>A purely visual overlay: never takes pointer input.</summary>
internal class StripBlock : Panel
{
    public StripBlock(UIContext context, TrackEditorView view) : base(context)
    {
        Width = 0;
        Height = 0;
        Background = new ColoredPlane { Color = TrackEditorView.StripSegmentA };
        Cursor = CursorType.Pointer;
        OnClick = _ =>
        {
            if (Segment != null) view._state.SelectSegment(Segment);
        };
    }

    public TrackSegment? Segment { get; set; }
}