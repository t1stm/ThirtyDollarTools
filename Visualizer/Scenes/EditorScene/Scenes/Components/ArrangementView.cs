using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The FL-style arrangement grid: channels are horizontal lanes, time runs right in
///     quarter notes at the root BPM. Every <see cref="TrackPlacement" /> is a draggable
///     clip; clicking an empty cell places the selected pattern there; Delete removes
///     the selected clip; the wheel pans horizontally. Double-clicking a clip fires
///     <see cref="OnOpenTrack" /> (the future per-track note editor).
/// </summary>
public class ArrangementView : Panel
{
    public const float LaneHeight = 44f;
    public const int MinChannels = 8;

    // ponytail: fixed pools — 128 bar lines cover a 3k px window at minimum zoom (6 ppq).
    // LaneLinePool is also the LaneHeader's toggle pool, so the two stay in step.
    private const int BarLinePool = 128;
    public const int LaneLinePool = 24;

    private static readonly Vector4 ClipColor = new(0.30f, 0.42f, 0.80f, 1f); // #4c6bcc
    private static readonly Vector4 SelectedClipColor = new(0.61f, 0.75f, 1f, 1f); // #9bc0ff
    private static readonly Vector4 LineColor = new(0.16f, 0.18f, 0.26f, 1f); // #292e42

    private static readonly Vector4 PlayheadColor = new(0.75f, 0.79f, 0.96f, 1f); // #c0caf5

    private readonly List<ClipBlock> _blocks = [];
    private readonly List<Panel> _barLines = [];
    private readonly List<Panel> _laneLines = [];
    private readonly Panel _playhead;
    private readonly EditorState _state;

    private ClipBlock? _dragging;
    private bool _refreshDeferred;
    private Vector4i? _inheritedClip;
    private float _scrollX;

    public ArrangementView(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        Focusable = true;
        Background = new ColoredPlane { Color = new Vector4(0.067f, 0.07f, 0.1f, 1f) };
        OnClick = _ => PlaceAtPointer();

        // Grid lines live at the front of Children so clips (added later) render above
        // them and win hit-test ties.
        for (var i = 0; i < LaneLinePool; i++) _laneLines.Add(NewLine(context));
        for (var i = 0; i < BarLinePool; i++) _barLines.Add(NewLine(context));
        foreach (var line in _laneLines) AddChild(line);
        foreach (var line in _barLines) AddChild(line);

        _playhead = new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Background = new ColoredPlane { Color = PlayheadColor }
        };
        AddChild(_playhead);

        Refresh();
    }

    /// <summary>
    ///     Playhead position on the arrangement timeline; anything negative hides it.
    ///     Driven every frame from playback, so it lays out its own line directly.
    /// </summary>
    public double PlayheadQuarters
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 1e-9) return;
            field = value;
            InvalidateLayout();
        }
    } = double.NegativeInfinity;

    /// <summary>Lane count, shared with the lane header so the M/S gutter stays aligned.</summary>
    public int Channels => ChannelCount;

    /// <summary>Horizontal zoom. Also the unit scale for hit-to-time math.</summary>
    public float PixelsPerQuarter { get; set; } = 24f;

    /// <summary>Drag/place snap, in quarter notes.</summary>
    public double SnapQuarterNotes { get; set; } = 1;

    /// <summary>While true (Ctrl held), the wheel zooms horizontally instead of panning.</summary>
    public bool WheelZooms { get; set; }

    /// <summary>Fired when a clip is double-clicked — the seam for the per-track editor.</summary>
    public Action<ProjectTrack>? OnOpenTrack { get; set; }

    private int ChannelCount
    {
        get
        {
            var deepest = 0;
            foreach (var placement in _state.Project.Placements)
                deepest = Math.Max(deepest, placement.Channel + 2); // one spare lane below
            return Math.Clamp(Math.Max(MinChannels, deepest), 1, LaneLinePool);
        }
    }

    /// <summary>Rebuilds the clip blocks from the model. Cheap; the clip count is human-scale.</summary>
    public void Refresh()
    {
        if (_dragging != null)
        {
            // A live drag mutates the model every snap crossing; rebuilding would
            // destroy the captured element and kill the drag. Positions sync in DoLayout.
            _refreshDeferred = true;
            InvalidateLayout();
            return;
        }

        foreach (var block in _blocks) RemoveChild(block);
        _blocks.Clear();
        foreach (var placement in _state.Project.Placements)
        {
            var block = new ClipBlock(Context, this, placement);
            _blocks.Add(block);
            AddChild(block);
        }

        // Keep the playhead last so it renders above the clips.
        RemoveChild(_playhead);
        AddChild(_playhead);

        RefreshSelection();
        InvalidateLayout();
    }

    public void RefreshSelection()
    {
        foreach (var block in _blocks)
            block.SetSelected(block.Placement == _state.SelectedPlacement);
    }

    protected override void DoLayout()
    {
        var lanes = ChannelCount;
        var width = Computed.Width;
        var lanesBottom = lanes * LaneHeight;

        for (var i = 0; i < _laneLines.Count; i++)
        {
            var visible = i < lanes && (i + 1) * LaneHeight <= Computed.Height;
            _laneLines[i].Width = visible ? width : 0;
            _laneLines[i].Height = 1;
            _laneLines[i].X = 0;
            _laneLines[i].Y = (i + 1) * LaneHeight;
        }

        var barWidth = 4 * PixelsPerQuarter;
        var firstBar = (int)Math.Floor(_scrollX / barWidth);
        for (var i = 0; i < _barLines.Count; i++)
        {
            var x = (firstBar + i) * barWidth - _scrollX;
            var visible = x >= 0 && x < width;
            _barLines[i].Width = visible ? 1 : 0;
            _barLines[i].Height = Math.Min(lanesBottom, Computed.Height);
            _barLines[i].X = x;
            _barLines[i].Y = 0;
        }

        foreach (var block in _blocks)
        {
            var placement = block.Placement;
            var quarters = placement.Track.DurationMinutes() * _state.Project.RootTiming.BPM;
            block.X = (float)(placement.StartQuarterNotes * PixelsPerQuarter) - _scrollX;
            block.Y = placement.Channel * LaneHeight + 2;
            block.Width = Math.Max(8, (float)(quarters * PixelsPerQuarter));
            block.Height = LaneHeight - 4;
        }

        var playheadX = (float)(PlayheadQuarters * PixelsPerQuarter) - _scrollX;
        var playheadVisible = PlayheadQuarters >= 0 && playheadX >= 0 && playheadX < width;
        _playhead.Width = playheadVisible ? 2 : 0;
        _playhead.Height = Math.Min(lanesBottom, Computed.Height);
        _playhead.X = playheadX;
        _playhead.Y = 0;

        base.DoLayout();
        ApplyClip(_inheritedClip);
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        if (WheelZooms)
        {
            // Zoom anchored at the pointer: the beat under the cursor stays put.
            var pointerPx = Context.PointerX - Computed.AbsoluteX;
            var anchorQuarters = (pointerPx + _scrollX) / PixelsPerQuarter;
            PixelsPerQuarter = Math.Clamp(PixelsPerQuarter * MathF.Pow(1.15f, scrollDelta.Y), 6f, 96f);
            _scrollX = Math.Max(0, anchorQuarters * PixelsPerQuarter - pointerPx);
        }
        else
        {
            _scrollX = Math.Max(0, _scrollX - scrollDelta.Y * 48f);
        }

        InvalidateLayout();
        return true;
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.Key is not (Keys.Delete or Keys.Backspace) || _state.SelectedPlacement is not { } selected)
            return base.HandleKeyDown(e);

        _state.RemovePlacement(selected);
        return true;
    }

    public override UIElement? HitTest(float x, float y)
    {
        // Clips are clipped to the view; scrolled-out ones must not take clicks.
        return !Visible || !ContainsPoint(x, y) ? null : base.HitTest(x, y);
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _inheritedClip = clip;
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var own = IntersectClip(new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height), clip);

        foreach (var child in Children) child.ApplyClip(own);
        Background?.ClipRect = clip;
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        if (_dragging == null || uiContext.CapturedElement == _dragging) return;

        // The drag ended (release or capture theft): run the rebuild Refresh skipped.
        // A plain click (press with no model change) skipped nothing — and must not
        // rebuild, or the second press of a double-click lands on a fresh ClipBlock
        // and UIContext's same-element check can never see a double-press.
        _dragging = null;
        if (!_refreshDeferred) return;
        _refreshDeferred = false;
        Refresh();
    }

    private void PlaceAtPointer()
    {
        if (_state.SelectedTrack is not { } track)
        {
            _state.SelectPlacement(null);
            return;
        }

        var (channel, start) = GridPosition(Context.PointerX, Context.PointerY, 0);
        _state.SelectPlacement(_state.PlaceTrack(track, channel, start));
    }

    private (int channel, double start) GridPosition(float x, float y, double grabOffsetQuarters)
    {
        var quarters = (x - Computed.AbsoluteX + _scrollX) / PixelsPerQuarter - grabOffsetQuarters;
        var snapped = Math.Max(0, Math.Round(quarters / SnapQuarterNotes) * SnapQuarterNotes);
        var channel = (int)Math.Floor((y - Computed.AbsoluteY) / LaneHeight);
        return (Math.Clamp(channel, 0, ChannelCount - 1), snapped);
    }

    /// <summary>A purely visual overlay: never takes pointer input.</summary>
    private class GhostPanel(UIContext context) : Panel(context)
    {
        public override UIElement? HitTest(float x, float y)
        {
            return null;
        }
    }

    private static Panel NewLine(UIContext context)
    {
        return new Panel(context)
        {
            Width = 0,
            Height = 1,
            Background = new ColoredPlane { Color = LineColor }
        };
    }

    private class ClipBlock : Panel
    {
        private readonly ColoredPlane _background;
        private readonly ArrangementView _view;
        private double _grabOffsetQuarters;

        public ClipBlock(UIContext context, ArrangementView view, TrackPlacement placement) : base(context)
        {
            _view = view;
            Placement = placement;
            Padding = 6;
            Background = _background = new ColoredPlane { Color = ClipColor };
            Children = [new Label(context, placement.Track.Name) { FontSizePx = 13f }];
            // Swallow the click so a release on a clip never bubbles into the view's
            // place-at-pointer handler; selection already happened on press.
            OnClick = _ => { };
        }

        public TrackPlacement Placement { get; }

        public override bool HandlePress(float x, float y)
        {
            _view._dragging = this;
            _grabOffsetQuarters = (x - Computed.AbsoluteX) / _view.PixelsPerQuarter;
            _view._state.SelectPlacement(Placement);
            _view._state.SelectTrack(Placement.Track);
            return true;
        }

        public override void HandlePointerDrag(float x, float y)
        {
            var (channel, start) = _view.GridPosition(x, y, _grabOffsetQuarters);
            _view._state.MovePlacement(Placement, channel, start);
            _view.InvalidateLayout();
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

        public void SetSelected(bool selected)
        {
            _background.Color = selected ? SelectedClipColor : ClipColor;
        }
    }
}
