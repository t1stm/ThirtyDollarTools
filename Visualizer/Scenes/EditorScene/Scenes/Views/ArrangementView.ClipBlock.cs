using EditorScene.State;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

public partial class ArrangementView
{
    /// <summary>
    ///     One clip's hit box. Its fill and its track name are slots in the view's batches
    ///     (see <see cref="DoLayout" />), so a project's clips cost no draw calls of their
    ///     own - which is also why <see cref="Selected" /> is a flag the layout reads rather
    ///     than a style class: there is no styled element to swap a background on.
    /// </summary>
    internal class ClipBlock : Panel
    {
        private readonly ArrangementView _view;
        private double _grabOffsetQuarters;

        public ClipBlock(UIContext context, ArrangementView view, TrackPlacement placement) : base(context)
        {
            _view = view;
            Placement = placement;
            Cursor = CursorType.Pointer;
            // Swallow the click so a release on a clip never bubbles into the view's
            // place-at-pointer handler; selection already happened on press.
            OnClick = _ => { };
        }

        public TrackPlacement Placement { get; }

        /// <summary>This clip's slot in the view's line batch - its position in the clip list.</summary>
        public required int BatchSlot { get; init; }

        /// <summary>Its slot in the clip-name batch, which has a range of its own.</summary>
        public required int LabelSlot { get; init; }

        /// <summary>Whether this clip is part of the selection - picks its fill in the layout.</summary>
        public bool Selected { get; set; }

        public override bool HandlePress(float x, float y)
        {
            _view._state.SelectTrack(Placement.Track);

            if (_view._state.ActiveTool == EditorTool.Select)
            {
                // Select tool: press-time selection only, matching the Draw tool's press
                // feel - no move drag, no BeginGesture.
                if (_view.FineSnap)
                    _view._state.RemoveFromPlacementSelection([Placement]); // Shift: remove (no-op if absent)
                else if (_view.WheelZooms)
                    _view._state.AddToPlacementSelection([Placement]); // Ctrl: append (no-op if present)
                else _view._state.SelectPlacement(Placement); // no modifier: replace
                return true;
            }

            _view._dragging = this;
            _view._state.BeginGesture();
            _grabOffsetQuarters = (x - Computed.AbsoluteX) / _view.PixelsPerQuarter;
            // A press onto a clip already in the selection keeps the selection, so the whole
            // group drags; pressing an unselected one replaces it with just this clip. Same
            // rule as a note press - see TrackEditorView.BeginNoteDrag.
            if (!_view._state.SelectedPlacements.Contains(Placement))
                _view._state.SelectPlacement(Placement);
            _view.BeginClipDrag(x, y, _grabOffsetQuarters);
            return true;
        }

        public override void HandlePointerDrag(float x, float y)
        {
            if (_view._state.ActiveTool == EditorTool.Select) return; // no-op: press already applied selection
            _view.UpdateClipDrag(x, y, _grabOffsetQuarters);
        }

        public override bool HandleDoublePress(float x, float y)
        {
            _view.OnOpenTrack?.Invoke(Placement.Track);
            return true;
        }

        /// <summary>Right-click removes the clip, same as selecting it and pressing Delete.</summary>
        public override bool HandleRightPress(float x, float y)
        {
            if (_view._dragging == this) return false;
            _view._state.RemovePlacement(Placement);
            return true;
        }
    }
}
