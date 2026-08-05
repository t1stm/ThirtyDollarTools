using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The FL-style arrangement grid: channels are horizontal lanes, time runs right in
///     quarter notes at the root BPM. Every <see cref="TrackPlacement" /> is a draggable
///     clip; clicking an empty cell places the selected pattern there; Delete removes
///     the selected clip; the wheel pans horizontally. Double-clicking a clip fires
///     <see cref="OnOpenTrack" /> (the future per-track note editor).
/// </summary>
public sealed class ArrangementView : Panel
{
    public const float LaneHeight = 44f;
    public const float RulerHeight = 18f;
    public const int MinChannels = 8;

    // ponytail: fixed pools - 128 bar lines cover a 3k px window at minimum zoom (6 ppq).
    // LaneLinePool is also the LaneHeader's toggle pool, so the two stay in step. It used
    // to be 24: past that many channels, ChannelCount's clamp (below) silently capped the
    // lane count these two pools iterate, so new channels still got a clip block (laid
    // out straight from placement.Channel, uncapped) but no divider line or M/S toggles.
    // 128 is generous headroom over any realistic track count.
    private const int BarLinePool = 128;
    public const int LaneLinePool = 128;

    // Loaded from the stylesheet - see EditorPalette and Scenes/Views/GridViews.snx.ss.
    private static Vector4 ClipColor => EditorPalette.Accent;
    private static Vector4 SelectedClipColor => EditorPalette.SelectionHighlight;
    private static Vector4 LineColor => EditorPalette.Surface;
    private static Vector4 RulerColor => EditorPalette.Panel;
    private static Vector4 LabelColor => EditorPalette.TextDim;

    // A property, not a static readonly field: a field would freeze whatever the palette
    // held when this type was first touched, which is only safe as long as that happens
    // after EditorPalette.Apply. Not worth the ordering trap.
    private static Vector4 PlayheadColor => EditorPalette.Playhead;

    private readonly List<Label> _barLabels = [];

    private readonly List<ClipBlock> _blocks = [];
    private readonly LineBatch _lineBatch = new();
    private readonly Panel _marqueeRect;

    private readonly ViewNavigation _nav = new(6f, 96f) { Zoom = 24f };
    private readonly Panel _playhead;
    private readonly Panel _rulerBackground;
    private readonly EditorState _state;
    private ClipBlock? _dragging;
    private Vector4i? _inheritedClip;
    private (double Quarters, double Channel)? _marqueeAnchor;
    private (double Quarters, double Channel)? _marqueeCursor;
    private MarqueeMode _marqueeMode;
    private bool _refreshDeferred;

    public ArrangementView(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        Focusable = true;
        Background = new ColoredPlane { Color = EditorPalette.GridBackground };
        OnClick = _ =>
        {
            var localY = context.PointerY - Computed.AbsoluteY;
            if (localY < RulerHeight)
            {
                OnSeekQuarters?.Invoke(Math.Max(0,
                    (context.PointerX - Computed.AbsoluteX + _nav.ScrollX) / PixelsPerQuarter));
                return;
            }

            // The Select tool never creates anything; its empty-lane press already
            // applied the marquee selection (see HandlePress/Update).
            if (_state.ActiveTool == EditorTool.Select) return;
            PlaceAtPointer();
        };

        // Grid lines render as one instanced draw call (see LineBatch) queued in
        // DrawSelf, below Children in the same depth layer, so clips still paint above.
        _lineBatch.Count = LaneLinePool + BarLinePool;

        // The beat ruler - same idea as the note editor's: a strip along the top
        // labeling every bar, highlighting whichever bar the playhead is in.
        _rulerBackground = new GhostPanel(context) { Background = new ColoredPlane { Color = RulerColor } };
        AddChild(_rulerBackground);
        for (var i = 0; i < BarLinePool; i++)
        {
            var label = new Label(context, "1") { Classes = ["grid-label"] };
            _barLabels.Add(label);
            AddChild(label);
        }

        _playhead = new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Background = new ColoredPlane { Color = PlayheadColor }
        };
        AddChild(_playhead);

        // Never takes input (GhostPanel), fill only, no border; re-added last on every
        // Refresh (with the playhead) so it renders above the clip blocks.
        _marqueeRect = new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Background = new ColoredPlane
                { Color = EditorPalette.MarqueeFill }
        };
        AddChild(_marqueeRect);

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

    // Test seam (internal - see EditorAssembly's InternalsVisibleTo("EditorScene.Tests")).
    internal IReadOnlyList<ClipBlock> Blocks => _blocks;

    /// <summary>
    ///     Vertical lane scroll, shared with the lane header so its M/S toggles track
    ///     the lanes they belong to.
    /// </summary>
    public float ScrollY => _nav.ScrollY;

    /// <summary>Horizontal zoom. Also the unit scale for hit-to-time math.</summary>
    public float PixelsPerQuarter
    {
        get => _nav.Zoom;
        set => _nav.Zoom = value;
    }

    /// <summary>Drag/place snap, in quarter notes.</summary>
    public double SnapQuarterNotes { get; set; } = 1;

    /// <summary>While true (Ctrl held), the wheel zooms horizontally instead of panning.</summary>
    public bool WheelZooms { get; set; }

    /// <summary>
    ///     While true (Shift held), a marquee drag removes contained clips from the
    ///     selection instead of replacing it. Mirrors <see cref="TrackEditorView.FineSnap" />.
    /// </summary>
    public bool FineSnap { get; set; }

    /// <summary>Fired when a clip is double-clicked - the seam for the per-track editor.</summary>
    public Action<ProjectTrack>? OnOpenTrack { get; set; }

    /// <summary>Fired with the clicked quarter-note position when the bar ruler is clicked.</summary>
    public Action<double>? OnSeekQuarters { get; set; }

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

    /// <summary>
    ///     Fired after any scroll/pan changes the viewport - the lane header listens
    ///     to keep its M/S toggles aligned with the lanes they belong to.
    /// </summary>
    public Action? OnScrolled { get; set; }

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

        // Keep the ruler (background, then its bar labels on top of that) above the
        // clips: a clip scrolled up past the top must be masked by the ruler, not paint
        // over it. Same "rely on paint order, not a clip rect" trick TrackEditorView uses
        // for its strip/ruler over notes bleeding past the grid's top edge.
        RemoveChild(_rulerBackground);
        AddChild(_rulerBackground);
        foreach (var label in _barLabels)
        {
            RemoveChild(label);
            AddChild(label);
        }

        // Keep the playhead and marquee rect last so they render above the clips.
        RemoveChild(_playhead);
        AddChild(_playhead);
        RemoveChild(_marqueeRect);
        AddChild(_marqueeRect);

        RefreshSelection();
        InvalidateLayout();
    }

    public void RefreshSelection()
    {
        foreach (var block in _blocks)
            block.SetSelected(_state.SelectedPlacements.Contains(block.Placement));
    }

    protected override void DoLayout()
    {
        var lanes = ChannelCount;
        var width = Computed.Width;
        var gridHeight = Math.Max(0, Computed.Height - RulerHeight);
        var lanesBottom = lanes * LaneHeight;

        // Recomputed every layout so the next wheel/pan clamps against the current bound,
        // but ScrollY itself is deliberately NOT re-clamped here (matching ScrollX, which
        // is never bounded on the right either - content can scroll past a shorter one).
        // Removing a track shrinks the lane count on every layout pass; eagerly pulling
        // ScrollY back down to fit auto-scrolled the view under a still-held pointer,
        // sliding whatever was next under a level-triggered right-press (or a delete-drag)
        // and cascading into deleting every track that had been off-screen below it.
        _nav.MaxScrollY = Math.Max(0, lanesBottom - gridHeight);
        var scrollY = _nav.ScrollY;
        var visibleLanesBottom = Math.Max(0, Math.Min(lanesBottom - scrollY, gridHeight));

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        for (var i = 0; i < LaneLinePool; i++)
        {
            var y = RulerHeight + (i + 1) * LaneHeight - scrollY;
            var visible = i < lanes && y >= RulerHeight && y <= Computed.Height;
            _lineBatch.Set(i, absX, absY + y, visible ? width : 0, 1, LineColor);
        }

        var playheadX = (float)(PlayheadQuarters * PixelsPerQuarter) - _nav.ScrollX;
        var playheadVisible = PlayheadQuarters >= 0 && playheadX >= 0 && playheadX < width;

        var barWidth = 4 * PixelsPerQuarter;
        var firstBar = (int)Math.Floor(_nav.ScrollX / barWidth);
        for (var i = 0; i < BarLinePool; i++)
        {
            var x = (firstBar + i) * barWidth - _nav.ScrollX;
            var visible = x >= 0 && x < width;
            _lineBatch.Set(LaneLinePool + i, absX + x, absY + RulerHeight, visible ? 1 : 0,
                visibleLanesBottom, LineColor);

            var label = _barLabels[i];
            if (!visible)
            {
                label.X = -1000f;
                continue;
            }

            var isCurrentBar = playheadVisible && playheadX >= x && playheadX < x + barWidth;
            label.Color = isCurrentBar ? PlayheadColor : LabelColor;
            label.SetTextContents($"{firstBar + i + 1}");
            label.X = x + 3;
            label.Y = (RulerHeight - 11f) / 2;
        }

        foreach (var block in _blocks)
        {
            var placement = block.Placement;
            var quarters = placement.Track.DurationMinutes() * _state.Project.RootTiming.BPM;
            block.X = (float)(placement.StartQuarterNotes * PixelsPerQuarter) - _nav.ScrollX;
            // No top clamp: a clip scrolled up past the ruler bleeds to a negative Y same
            // as a note bleeds past TrackEditorView's grid top, masked by the ruler's paint
            // order (see Refresh) rather than clamped - clamping it here pinned every clip
            // passing through the ruler band to the same Y, stacking their labels together.
            block.Y = RulerHeight + placement.Channel * LaneHeight + 2 - scrollY;
            block.Width = Math.Max(8, (float)(quarters * PixelsPerQuarter));
            block.Height = LaneHeight - 4;
        }

        _playhead.Width = playheadVisible ? 2 : 0;
        _playhead.Height = visibleLanesBottom;
        _playhead.X = playheadX;
        _playhead.Y = RulerHeight;

        LayoutMarquee();

        _rulerBackground.X = 0;
        _rulerBackground.Y = 0;
        _rulerBackground.Width = width;
        _rulerBackground.Height = RulerHeight;

        base.DoLayout();
        ApplyClip(_inheritedClip);
    }

    /// <summary>
    ///     Positions the marquee rectangle from its model-space anchor/cursor every
    ///     frame, so it scroll-corrects; zero size (hidden) while no marquee is active.
    /// </summary>
    private void LayoutMarquee()
    {
        if (_marqueeAnchor is not { } anchor || _marqueeCursor is not { } cursor)
        {
            _marqueeRect.Width = 0;
            _marqueeRect.Height = 0;
            return;
        }

        var x1 = (float)(anchor.Quarters * PixelsPerQuarter) - _nav.ScrollX;
        var x2 = (float)(cursor.Quarters * PixelsPerQuarter) - _nav.ScrollX;
        var y1 = RulerHeight + (float)(anchor.Channel * LaneHeight) - _nav.ScrollY;
        var y2 = RulerHeight + (float)(cursor.Channel * LaneHeight) - _nav.ScrollY;

        _marqueeRect.X = Math.Min(x1, x2);
        _marqueeRect.Y = Math.Min(y1, y2);
        _marqueeRect.Width = Math.Abs(x2 - x1);
        _marqueeRect.Height = Math.Abs(y2 - y1);
    }

    /// <summary>Continuous, unsnapped quarter-note position - the marquee's counterpart to <see cref="GridPosition" />.</summary>
    private double UnsnappedQuartersAt(float localX)
    {
        return (localX + _nav.ScrollX) / PixelsPerQuarter;
    }

    /// <summary>Continuous, unsnapped channel - the marquee's counterpart to <see cref="GridPosition" />.</summary>
    private double UnsnappedChannelAt(float localY)
    {
        return (localY - RulerHeight + _nav.ScrollY) / LaneHeight;
    }

    /// <summary>
    ///     Select tool only: starts a marquee on an empty-lane press (below the ruler).
    ///     Empty-lane presses otherwise reach only <c>OnClick</c> on release, but the
    ///     marquee needs the press to capture the pointer for the drag.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        var localY = y - Computed.AbsoluteY;
        if (localY < RulerHeight) return false; // ruler seek still runs on release via OnClick
        if (_state.ActiveTool != EditorTool.Select) return false; // Draw tool: PlaceAtPointer on release, unchanged

        var localX = x - Computed.AbsoluteX;
        _marqueeAnchor = _marqueeCursor = (UnsnappedQuartersAt(localX), UnsnappedChannelAt(localY));
        _marqueeMode = FineSnap ? MarqueeMode.Remove : WheelZooms ? MarqueeMode.Append : MarqueeMode.Replace;
        InvalidateLayout();
        return true; // capture: the drag updates the marquee rect
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_marqueeAnchor == null) return;
        _marqueeCursor = (UnsnappedQuartersAt(x - Computed.AbsoluteX), UnsnappedChannelAt(y - Computed.AbsoluteY));
        InvalidateLayout();
    }

    /// <summary>
    ///     Applies the marquee's Replace/Append/Remove modifier semantics against every
    ///     placement whose time span intersects the marquee's quarter range and whose
    ///     channel is within its row range - span-intersection (not containment), since
    ///     clips are wide objects and a marquee merely touching one should select it.
    /// </summary>
    private void CommitMarquee()
    {
        if (_marqueeAnchor is not { } anchor || _marqueeCursor is not { } cursor) return;

        var minQ = Math.Min(anchor.Quarters, cursor.Quarters);
        var maxQ = Math.Max(anchor.Quarters, cursor.Quarters);
        var minChannel = Math.Min(anchor.Channel, cursor.Channel);
        var maxChannel = Math.Max(anchor.Channel, cursor.Channel);

        var contained = new List<TrackPlacement>();
        foreach (var placement in _state.Project.Placements)
        {
            var quarters = placement.Track.DurationMinutes() * _state.Project.RootTiming.BPM;
            var end = placement.StartQuarterNotes + quarters;
            if (end <= minQ || placement.StartQuarterNotes >= maxQ) continue;

            // The lane is a whole rendered row (Channel .. Channel+1), not a point -
            // comparing the marquee's box against the bare Channel int only ever
            // matched when the box touched the lane's top edge, missing anything
            // dragged entirely inside the row (same bug class as the note editor's
            // marquee, just on this axis instead of the time axis above).
            if (placement.Channel >= maxChannel || placement.Channel + 1 <= minChannel) continue;

            contained.Add(placement);
        }

        switch (_marqueeMode)
        {
            case MarqueeMode.Append:
                _state.AddToPlacementSelection(contained);
                break;
            case MarqueeMode.Remove:
                _state.RemoveFromPlacementSelection(contained);
                break;
            default:
                _state.SetPlacementSelection(contained); // empty marquee clears the selection
                break;
        }
    }

    /// <summary>
    ///     Plain wheel scrolls lanes vertically; Shift+wheel pans time instead -
    ///     same binding as <see cref="TrackEditorView.HandleScroll" />'s FineSnap/panXWithY.
    /// </summary>
    public override bool HandleScroll(Vector2 scrollDelta)
    {
        var pointerPx = Context.PointerX - Computed.AbsoluteX;
        _nav.Wheel(scrollDelta, WheelZooms, FineSnap, pointerPx);
        InvalidateLayout();
        OnScrolled?.Invoke();
        return true;
    }

    /// <summary>
    ///     FL-style middle-mouse pan of both axes, fed per frame from the scene's mouse
    ///     handler (the framework only routes left/right buttons). A hold that starts
    ///     inside the view drags the viewport with the pointer until release.
    /// </summary>
    public void MiddlePan(bool held, float x, float y)
    {
        if (!_nav.MiddlePan(held, x, y, ContainsPoint(x, y))) return;
        InvalidateLayout();
        OnScrolled?.Invoke();
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Delete or Keys.Backspace when _state.SelectedPlacements.Count > 0:
                _state.DeleteSelection();
                return true;
            case Keys.C when e.Control:
                _state.CopySelection();
                return true;
            case Keys.V when e.Control:
                _state.Paste();
                return true;
            case Keys.X when e.Control:
                _state.CutSelection();
                return true;
            case Keys.A when e.Control:
                _state.SelectAll();
                return true;
            case Keys.Escape when _state.SelectedPlacements.Count > 0:
                _state.ClearSelection();
                return true;
            default:
                return base.HandleKeyDown(e);
        }
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
        var right = x + (int)Computed.Width;
        var bottom = y + (int)Computed.Height;
        var own = IntersectClip(new Vector4i(x, y, right, bottom), clip);

        // Everything that lives in the lane grid is confined below the ruler band, not
        // just to the view: Refresh's re-add order only stacks siblings within one Index
        // layer, and a ClipBlock's name Label sits a layer deeper (Panel.Index cascades
        // +1 per level) - deeper layers render after shallower ones, so the track name
        // painted straight over the ruler background and its bar numbers.
        var lanes = IntersectClip(new Vector4i(x, y + (int)RulerHeight, right, bottom), clip);

        foreach (var child in Children) child.ApplyClip(lanes);
        _rulerBackground.ApplyClip(own);
        foreach (var label in _barLabels) label.ApplyClip(own);

        Background?.ClipRect = clip;
        _lineBatch.ClipRect = lanes;
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        ctx.QueueRender(_lineBatch, Index);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        Context.DequeueRender(_lineBatch, Index);
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);

        if (_marqueeAnchor != null && uiContext.CapturedElement != this)
        {
            CommitMarquee();
            _marqueeAnchor = _marqueeCursor = null;
            InvalidateLayout();
        }

        if (_dragging == null || uiContext.CapturedElement == _dragging) return;

        // The drag ended (release or capture theft): run the rebuild Refresh skipped.
        // A plain click (press with no model change) skipped nothing - and must not
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
        var quarters = (x - Computed.AbsoluteX + _nav.ScrollX) / PixelsPerQuarter - grabOffsetQuarters;
        var snapped = Math.Max(0, Math.Round(quarters / SnapQuarterNotes) * SnapQuarterNotes);
        var channel = (int)Math.Floor((y - Computed.AbsoluteY - RulerHeight + _nav.ScrollY) / LaneHeight);
        return (Math.Clamp(channel, 0, ChannelCount - 1), snapped);
    }

    // Marquee (Select tool): model-space anchor/cursor (continuous quarters, channel),
    // so mid-drag panning can't corrupt it. Mode is sampled from the modifier bools at
    // press, applied at release. See TrackEditorView for the identical note-editor version.
    private enum MarqueeMode
    {
        Replace,
        Append,
        Remove
    }

    /// <summary>A purely visual overlay: never takes pointer input.</summary>
    internal class ClipBlock : Panel
    {
        private readonly ColoredPlane _background;
        private readonly ArrangementView _view;
        private double _grabOffsetQuarters;

        public ClipBlock(UIContext context, ArrangementView view, TrackPlacement placement) : base(context)
        {
            _view = view;
            Placement = placement;
            Classes = ["clip-block"];
            // Code-owned fill: Refresh tints it per selection state on every layout.
            Background = _background = new ColoredPlane { Color = ClipColor };
            Children = [new Label(context, placement.Track.Name) { Classes = ["clip-label"] }];
            // Swallow the click so a release on a clip never bubbles into the view's
            // place-at-pointer handler; selection already happened on press.
            OnClick = _ => { };
        }

        public TrackPlacement Placement { get; }

        public override bool HandlePress(float x, float y)
        {
            _view._state.SelectTrack(Placement.Track);

            if (_view._state.ActiveTool == EditorTool.Select)
            {
                // Select tool: press-time selection only, matching the Draw tool's press
                // feel - no move drag, no BeginGesture (dragging a multi-selection is a
                // listed future extension).
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
            _view._state.SelectPlacement(Placement);
            return true;
        }

        public override void HandlePointerDrag(float x, float y)
        {
            if (_view._state.ActiveTool == EditorTool.Select) return; // no-op: press already applied selection
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