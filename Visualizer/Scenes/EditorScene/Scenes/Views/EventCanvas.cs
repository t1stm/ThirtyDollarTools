using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using VisualizerScene.Objects;
using VisualizerScene.Objects.Playfield;
using VisualizerScene.Objects.Sound_Values;
using VisualizerScene.Objects.Playfield.Batch;
using VisualizerScene.Objects.Playfield.Batch.Chunks;

namespace EditorScene.Scenes.Views;

/// <summary>
///     A block of TDW events drawn with the playfield's own look - icons, value/volume/pan
///     badges, cut lists, "!bg" swatches - but living inside the UI tree instead of under the
///     visualizer's camera, so it can sit in a <see cref="Sundex.Components.Scroll.ScrollView" />.
///     One element, three usage sites: an instrument's sounds in the palette, the action
///     palette's grid, and the faithful sequence itself.
///     The renderables carry absolute positions (that is how the playfield draws them), so a
///     scroll has to move every one of them - see <see cref="Reposition" />.
///     Chunks that don't reach the visible rectangle are not queued at all - see
///     <see cref="RefreshCulling" />; an imported cover is thousands of events long and a
///     queued chunk costs a frame whether it is on screen or not.
///     Moving costs nothing per slot either: a scroll shifts the block's bounds and the
///     recorded per-chunk layout states, and a chunk's renderables are only put where they
///     belong once it is about to be drawn - see <see cref="ShiftBy" /> and
///     <see cref="CatchUp" />.
/// </summary>
public sealed class EventCanvas : Panel
{
    /// <summary>The site's gap-to-icon ratio, kept at every box size - see <see cref="NewLayout" />.</summary>
    private readonly float _marginRatio;

    private readonly PlayfieldSettings _settings;
    private readonly PlayfieldSizing _sizing;
    private readonly float _size;
    private float _margin;
    private List<PlayfieldChunk> _chunks = [];
    private BaseEvent[] _events = [];
    private ChunkGenerator? _generator;

    /// <summary>The box side the current layout used; see <see cref="NewLayout" />.</summary>
    private float _boxSize;

    /// <summary>The box side the current chunks were generated at - their badge text is baked at it.</summary>
    private float _generatedBoxSize;

    /// <summary>The slot a drag started on, and whether it has actually moved off it.</summary>
    private int? _dragIndex;

    /// <summary>
    ///     What the drag is carrying - its <see cref="GroupOf" /> group, so the slot it now
    ///     occupies can be re-found after every move. Null when the canvas has no groups.
    /// </summary>
    private object? _dragGroup;

    /// <summary>
    ///     What the last move landed on: the target's group where there is one, its slot
    ///     index otherwise. The drag does not fire again while the pointer stays on it - the
    ///     dragged item has already gone past it, so a second move would only swap them back,
    ///     which is what a stationary pointer (or a sweep across a wide neighbour) would do.
    /// </summary>
    private object? _dragOver;

    private bool _dragged;

    /// <summary>The lit panel behind a hovered or selected slot; one per lit slot, pooled.</summary>
    private readonly List<ColoredPlane> _highlights = [];

    /// <summary>The slot under the pointer, or null - refreshed every frame while hovered.</summary>
    private int? _hovered;

    /// <summary>The slots the hovered one lights - its whole group; see <see cref="HoverRun" />.</summary>
    private (int Start, int End)? _hoverRun;

    // $theme.surface_raised, $theme.accent and $theme.accent_hover. Copied rather than read:
    // these paint renderables inside the canvas, not elements the stylesheet can reach.
    private static readonly Vector4 HoverFill = new(0.208f, 0.227f, 0.329f, 1f);
    private static readonly Vector4 SelectedFill = new(0.298f, 0.420f, 0.800f, 1f);
    private static readonly Vector4 SelectedHoverFill = new(0.420f, 0.510f, 0.769f, 1f);

    /// <summary>How far a bounce lifts a sound, as a fraction of its box - BounceAnimation's own figure.</summary>
    private const float BounceHeight = 1f / 4.2666667f;

    /// <summary>
    ///     Events per chunk. A chunk is the unit <see cref="RefreshCulling" /> keeps or drops,
    ///     and it draws every renderable it holds - so the playfield's 512 (thirty-two lines
    ///     at sixteen across) is far coarser than a panel that shows about seven. 128 is eight
    ///     lines: tight enough that the culled block is close to what is on screen, coarse
    ///     enough not to multiply the draw calls.
    /// </summary>
    private const int CullChunkSize = 128;

    private int _handledRightPressId = -1;

    /// <summary>Guards the Height write in <see cref="Reposition" /> from re-entering through layout.</summary>
    private bool _positioning;

    /// <summary>
    ///     The scissor rectangle this canvas draws inside - the scroller's, via
    ///     <see cref="ApplyClip" />. What <see cref="RefreshCulling" /> culls against; null
    ///     means nothing is clipping it, so everything is on screen.
    /// </summary>
    private Vector4i? _clip;

    /// <summary>The chunks currently queued for rendering, as an inclusive range. Empty is (0, -1).</summary>
    private (int First, int Last) _queued = (0, -1);

    /// <summary>Where the block was last laid out - see <see cref="Reposition" />'s early-out.</summary>
    private (float X, float Y, float Width, float Box) _laidOutAt = (float.NaN, float.NaN, 0, 0);

    /// <summary>
    ///     The layout state each chunk starts from, recorded by the last full pass. With these
    ///     a chunk can be positioned on its own - see <see cref="CatchUp" />.
    /// </summary>
    private (int SoundIndex, float Y, float Height)[] _seek = [];

    /// <summary>
    ///     Chunks whose renderables are still sitting where the last scroll left them. A scroll
    ///     moves the whole block by one number, so it is applied to the cheap things (a chunk's
    ///     bounds, the seek states, the extent) straight away and to the renderables only when
    ///     a chunk is actually about to be drawn or read.
    /// </summary>
    private bool[] _stale = [];

    /// <summary>The block's far edge from the last full pass, shifted along with the scroll.</summary>
    private (float Right, float Bottom) _extent;

    /// <param name="size">One box's side in pixels - the playfield's SoundSize for this view.</param>
    /// <param name="margin">The gap between two boxes, split evenly across their sides.</param>
    /// <param name="perLine">
    ///     Fixed number of boxes per line. Null derives it from the element's own width, which
    ///     is what a view that wraps (the sequence, the action grid) wants.
    /// </param>
    public EventCanvas(UIContext context, PlayfieldSettings settings, float size, float margin = 6f,
        int? perLine = null) : base(context)
    {
        _size = size;
        _margin = margin;
        _marginRatio = margin / size;
        // Its own sizing, tracking the box this canvas actually draws at: the shared look's
        // is the visualizer's, and a value/volume badge sized for that reads far too small
        // (or too large) at every other box size.
        _sizing = new PlayfieldSizing((int)size);
        _settings = new PlayfieldSettings
        {
            SampleHolder = settings.SampleHolder,
            AtlasStore = settings.AtlasStore,
            Fonts = settings.Fonts,
            RenderScale = settings.RenderScale,
            PlayfieldSizing = _sizing
        };
        PerLine = perLine;
        Computed = new ComputedRectangle { OnUpdate = Reposition };
        UpdateCursorOnHover = true;

        // Clicks carry no coordinates, so the box under the pointer is resolved here - the
        // canvas is one element, not one per event (sixteen actions would be sixteen GL
        // buffer sets).
        OnClick = _ =>
        {
            // A release that ended a drag is not a click - picking here would delete the
            // slot that was just moved.
            if (_dragged)
            {
                _dragged = false;
                return;
            }

            if (IndexAt(Context.PointerX, Context.PointerY) is { } index) OnPick?.Invoke(index);
        };
    }

    /// <summary>Fired with the index of the clicked event.</summary>
    public Action<int>? OnPick { get; set; }

    /// <summary>Fired with the index of the right-clicked event - preview, everywhere it is wired.</summary>
    public Action<int>? OnPreview { get; set; }

    /// <summary>Fired with the index of the scrolled event and the notch count (up is positive).</summary>
    public Action<int, int>? OnAdjust { get; set; }

    /// <summary>
    ///     Fired while a slot is dragged onto another one, with the dragged and the hovered
    ///     index. Null leaves this canvas click-only; setting it makes it capture presses.
    /// </summary>
    public Action<int, int>? OnMove { get; set; }

    /// <summary>
    ///     Whether the event at an index is selected - it gets a lit panel behind it. Null
    ///     leaves the canvas with hover lighting only.
    /// </summary>
    public Func<int, bool>? IsSelected { get; set; }

    /// <summary>
    ///     Whether the slot under the pointer lights up. Off where the row, not the slot, is
    ///     what a click hits - a palette row, whose own fill does the lighting.
    /// </summary>
    public bool HighlightHover { get; set; } = true;

    /// <summary>
    ///     What a slot belongs to, so hovering one of a group lights the whole group - a
    ///     layered instrument is several "!combine"-joined slots but one thing to click.
    ///     Compared by reference; null leaves every slot on its own.
    /// </summary>
    public Func<int, object?>? GroupOf { get; set; }

    /// <summary>
    ///     Leaves a bounce's worth of room above the first line. A bounce lifts a sound by a
    ///     quarter of its box, and the view that hosts this canvas scissors to its own rect -
    ///     without the inset the top line's bounce is simply cut off.
    /// </summary>
    public bool ReserveBounceRoom { get; set; }

    /// <summary>Null derives the count from the element's width every layout; see the constructor.</summary>
    public int? PerLine { get; set; }

    /// <summary>
    ///     Whether a "!divider" breaks the line, as it does on the playfield. True for the
    ///     sequence, where that is the action's whole point; false for the palettes, where the
    ///     divider is just one entry in a grid and must not tear it in half.
    /// </summary>
    public bool BreakOnDividers { get; set; } = true;

    /// <summary>
    ///     With a fixed <see cref="PerLine" />, shrinks the box so exactly that many fill the
    ///     element's width - never above the constructor's size. The website lays its sequence
    ///     out sixteen across whatever the page is wide, and sixteen boxes at the full size
    ///     don't fit a panel; this keeps the count and gives up the pixels.
    /// </summary>
    public bool FitPerLine { get; set; }

    /// <summary>
    ///     The size shared with the rest of the faithful editor. A <see cref="FitPerLine" />
    ///     canvas writes what it worked out into it; every other canvas reads it, so the
    ///     palettes and the sequence draw at one scale. Null leaves this canvas on its own
    ///     constructor size.
    /// </summary>
    public FaithfulScale? Scale { get; set; }

    /// <summary>The events currently drawn, in the order they were given.</summary>
    public IReadOnlyList<BaseEvent> Events => _events;

    public override ComputedRectangle Computed { get; protected set; }

    /// <summary>
    ///     Replaces what this canvas draws. The old chunks' GL buffers are released here -
    ///     nothing else owns them.
    ///     Returns whether anything other than a sound's own badges changed - a caller that
    ///     derives timing from these events (the faithful sequence's play schedule) can keep
    ///     what it had when this is false.
    /// </summary>
    public bool SetEvents(IEnumerable<BaseEvent> events)
    {
        var next = events as BaseEvent[] ?? [.. events];
        if (TryPatch(next) is { } soundsOnly) return !soundsOnly;

        // Dequeue before disposing: the render queue holds the chunk by reference, and a
        // disposed one still in it draws from freed buffers.
        foreach (var chunk in _chunks)
        {
            Context.DequeueRender(chunk, Index);
            chunk.Dispose();
        }

        _chunks = [];
        _queued = (0, -1);
        _laidOutAt = (float.NaN, float.NaN, 0, 0);
        _seek = [];
        _stale = [];

        _events = next;
        if (_events.Length == 0)
        {
            _generator = null;
            // An empty sequence still decides the scale - otherwise the palettes sit at the
            // full size until the first item is added and then jump.
            if (FitPerLine) NewLayout();
            Height = 0;
            return true;
        }

        _generator = new ChunkGenerator(_settings, NewLayout()) { ChunkSize = CullChunkSize };
        _generatedBoxSize = _boxSize;
        _chunks = _generator.GenerateChunks(_events);

        if (!BreakOnDividers)
            foreach (var chunk in _chunks)
                foreach (var renderable in chunk.Renderables)
                    renderable.IsDivider = false;

        Reposition();

        // Queueing happens in DrawSelf, which only runs on a draw pass - the fresh chunks
        // would otherwise not be drawn until something else redrew this element. Only while
        // this canvas is actually on screen, though: the render queue ignores tree
        // attachment, so a detached panel that rebuilt itself (the palette follows every
        // project change, open or not) would paint its icons over whatever replaced it.
        if (Drawn) DrawTo(Context);
        return true;
    }

    /// <summary>
    ///     Redraws an edit without rebuilding the sequence. Every mutation comes back through
    ///     <see cref="SetEvents" /> with a freshly expanded stream, and regenerating every
    ///     chunk's renderables, text and GL buffers for it is ~740 ms on an imported cover -
    ///     for one scrolled value. Slots that draw the same are left exactly as they are and
    ///     only the chunks holding a changed one are built again.
    ///     Null when the block's shape moved and the whole thing has to be rebuilt; otherwise
    ///     whether every slot that changed was a sound (so the caller's timing still holds).
    /// </summary>
    private bool? TryPatch(BaseEvent[] next)
    {
        if (_generator is null || _chunks.Count == 0 || next.Length != _events.Length) return null;

        var soundsOnly = true;
        var dirty = new List<int>();
        for (var i = 0; i < next.Length; i++)
        {
            if (SameDrawn(_events[i], next[i])) continue;
            // A divider appearing or leaving breaks a line, which moves every slot after it.
            if (IsDivider(_events[i]) != IsDivider(next[i])) return null;

            if (IsAction(next[i]) || IsAction(_events[i])) soundsOnly = false;

            var chunk = i / _generator.ChunkSize;
            if (dirty.Count == 0 || dirty[^1] != chunk) dirty.Add(chunk);
        }

        _events = next;
        if (dirty.Count == 0) return soundsOnly;

        foreach (var index in dirty)
        {
            Context.DequeueRender(_chunks[index], Index);
            _chunks[index].Dispose();

            var chunk = _generator.GenerateChunk(next, index);
            if (!BreakOnDividers)
                foreach (var renderable in chunk.Renderables)
                    renderable.IsDivider = false;

            chunk.ClipRect = _clip;
            _chunks[index] = chunk;
            _generator.PositionChunk(chunk, _seek[index]);
            _stale[index] = false;
        }

        // The replaced chunks are new objects, so whatever was queued for them is gone with
        // them - let culling put the visible ones back.
        _queued = (0, -1);
        if (Drawn) RefreshCulling();
        RefreshSelection();
        return soundsOnly;
    }

    /// <summary>Whether two events draw the same - icon, badges, and a cut's list of sounds.</summary>
    private static bool SameDrawn(BaseEvent a, BaseEvent b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.GetType() != b.GetType() || a.SoundEvent != b.SoundEvent) return false;
        if (a.Value != b.Value || a.WorkingValue != b.WorkingValue) return false;
        if (!Nullable.Equals(a.Volume, b.Volume)) return false;

        if (a is ExtendedEvent left && b is ExtendedEvent right &&
            (left.Pan != right.Pan || left.OffsetInSeconds != right.OffsetInSeconds)) return false;

        return a is not IndividualCutEvent cut || (b is IndividualCutEvent other &&
                                                   cut.CutSounds.SetEquals(other.CutSounds));
    }

    private static bool IsDivider(BaseEvent ev)
    {
        return ev.SoundEvent == "!divider";
    }

    /// <summary>Exactly the walk's classification: an action never advances the clock.</summary>
    private static bool IsAction(BaseEvent ev)
    {
        return (ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent;
    }

    /// <summary>
    ///     The index of the event whose cell contains the point, or null. The cell is the
    ///     whole box plus its margin - the very rectangle a hover lights up - not the
    ///     texture: a sound is drawn centred and aspect-corrected inside its box, so a
    ///     texture-sized hit area leaves gaps where a hover, a click or a scroll lands on
    ///     nothing while the pointer is plainly on the slot.
    ///     Coordinates are the absolute ones the input routing hands to <c>HandlePress</c>.
    /// </summary>
    public int? IndexAt(float x, float y)
    {
        var index = 0;
        for (var c = 0; c < _chunks.Count; c++)
        {
            var chunk = _chunks[c];
            // Off-screen slots can't be under the pointer, and walking them is what made this
            // cost the whole sequence on every hover frame.
            if (c < _queued.First || c > _queued.Last)
            {
                index += chunk.Renderables.Length;
                continue;
            }

            foreach (var renderable in chunk.Renderables)
            {
                // A null SoundEvent leaves an unbuilt renderable in place; it draws nothing
                // and must not swallow the click either.
                if (index < _events.Length && _events[index].SoundEvent is not null)
                {
                    var (bx, by) = BoxOf(renderable);
                    if (x >= bx - _margin / 2 && x < bx + _boxSize + _margin / 2 &&
                        y >= by - _margin / 2 && y < by + _boxSize + _margin / 2) return index;
                }

                index++;
            }
        }

        return null;
    }

    /// <summary>
    ///     The slot a drag at this point lands on: the cell under the pointer, or the last
    ///     slot once the pointer has left the block past its end. Dropping past the end is
    ///     the natural way to move something to the end of the sequence, and a fast drag
    ///     leaves the last cell in a single frame - without this the item stops one short.
    /// </summary>
    private int? DragTarget(float x, float y)
    {
        if (IndexAt(x, y) is { } index) return index;
        if (_events.Length == 0 || BoxAt(_events.Length - 1) is not { } last) return null;

        var belowLastLine = y >= last.Y + _boxSize + _margin / 2;
        var afterLastSlot = y >= last.Y - _margin / 2 && x >= last.X + _boxSize + _margin / 2;
        return belowLastLine || afterLastSlot ? _events.Length - 1 : null;
    }

    /// <summary>
    ///     The single entry point for sizing, so <see cref="Extent" /> can't measure against a
    ///     different box than the layout used. A fitted canvas derives the size and publishes
    ///     it; every other one takes whatever the shared <see cref="Scale" /> holds.
    /// </summary>
    private LayoutHandler NewLayout()
    {
        if (FitPerLine && PerLine is { } fitted && fitted > 0)
        {
            // The margin rides on the box (below), so the fit solves box * (1 + ratio) = width / count.
            _boxSize = Math.Min(_size, Math.Max(8, Computed.Width / fitted / (1 + _marginRatio)));
            // Not before the first layout: a zero width would publish a nonsense size and
            // every follower would redraw at it for a frame.
            if (Scale is not null && Computed.Width > 1) Scale.BoxSize = _boxSize;
        }
        else
        {
            _boxSize = Scale?.BoxSize ?? _size;
        }

        // Both follow the box: a fixed gap swamps a shrunken icon, and the badge font sizes
        // are derived from SoundSize by the setter.
        _margin = _boxSize * _marginRatio;
        _sizing.SoundSize = (int)MathF.Round(_boxSize);

        var perLine = PerLine ?? Math.Max(1, (int)((Computed.Width + _margin) / (_boxSize + _margin)));
        var top = ReserveBounceRoom ? _boxSize * BounceHeight : 0;
        return new LayoutHandler(_boxSize, perLine, new GapBox(_margin / 2),
            new GapBox(Computed.AbsoluteX, Computed.AbsoluteY + top, 0, 0));
    }

    /// <summary>
    ///     Re-lays the block out at the shared <see cref="Scale" />'s current size. Pushed by
    ///     <see cref="FaithfulScale.Changed" />: a box size is not part of the rectangle, so
    ///     <see cref="Reposition" />'s own trigger never fires for a size change alone.
    /// </summary>
    public void RefreshScale()
    {
        Reposition();
    }

    /// <summary>
    ///     Re-lays the block out at the element's current absolute origin - fired whenever
    ///     the rectangle moves, which is what a scroll is. Also the only place the element's
    ///     size is set: the height is content-driven either way, and a canvas with a fixed
    ///     <see cref="PerLine" /> takes its width from the content too, so it doesn't stretch
    ///     across a row and swallow clicks meant for the row itself.
    /// </summary>
    private void Reposition()
    {
        if (_generator is null || _positioning || _chunks.Count == 0) return;

        // Layout recomputes the rectangle whether or not it moved, and positioning the block
        // is O(events) - an imported cover's worth of that, every frame, is most of what a
        // long sequence used to cost. Nothing to do when it would land where it already is.
        var at = (Computed.AbsoluteX, Computed.AbsoluteY, Computed.Width, Scale?.BoxSize ?? _size);
        if (at == _laidOutAt) return;

        // Only the origin moved: the block's shape is unchanged, so every position shifts by
        // exactly that much. The bounds and the seek states take it now (a few hundred of
        // those) and the renderables take it when they are next needed (tens of thousands).
        // Laying all of them out again is what made one scroll frame 140 ms.
        var shifted = !float.IsNaN(_laidOutAt.X) && at.Item1 == _laidOutAt.X &&
                      at.Item3 == _laidOutAt.Width && at.Item4 == _laidOutAt.Box;
        var dy = at.Item2 - _laidOutAt.Y;
        _laidOutAt = at;

        _positioning = true;
        _generator.LayoutHandler = NewLayout();

        // Badge text is baked at its font size when a chunk is generated, so a box size that
        // actually moved needs the chunks rebuilt rather than only shifted.
        if (Math.Abs(_boxSize - _generatedBoxSize) > 0.5f)
        {
            _positioning = false;
            SetEvents(_events);
            return;
        }

        if (shifted) ShiftBy(dy);
        else MeasureAll();

        var (right, bottom) = _extent;
        Height = bottom - Computed.AbsoluteY;
        // A fitted canvas keeps whatever width it was given - that is what it fitted to.
        if (PerLine is not null && !FitPerLine) Width = right - Computed.AbsoluteX;
        _positioning = false;

        RefreshCulling();
        RefreshSelection();
    }

    /// <summary>
    ///     Repaints the lit panels: one behind the hovered group, one behind each selected
    ///     one. A panel rather than the icon's own alpha, so what lights up is what you are
    ///     pointing at right now - not whatever the last edit happened to touch.
    ///     Neighbours in the same state merge into a single panel, and a line break ends it,
    ///     so a group that wraps reads as one rectangle per line rather than one per slot.
    /// </summary>
    public void RefreshSelection()
    {
        _hoverRun = HoverRun();

        var lit = 0;
        // Only what is on screen: a panel behind an off-screen slot is scissored away anyway,
        // and Lit() walks the chunks to find a slot's box, so the whole sequence's worth of
        // that is O(events x chunks) for nothing.
        var (index, visibleEnd) = VisibleEvents();
        while (index <= visibleEnd)
        {
            if (Lit(index) is not { } first)
            {
                index++;
                continue;
            }

            var last = first;
            while (index + 1 <= visibleEnd && Lit(index + 1) is { } next &&
                   next.Selected == first.Selected && next.Hovered == first.Hovered &&
                   Math.Abs(next.Y - first.Y) < 0.5f)
            {
                last = next;
                index++;
            }

            var plane = PlaneAt(lit++);
            plane.Color = first.Selected ? first.Hovered ? SelectedHoverFill : SelectedFill : HoverFill;
            plane.BorderRadius = _boxSize * 0.15f;
            plane.Position = (first.X - _margin / 2, first.Y - _margin / 2, 0);
            plane.Scale = (last.X - first.X + _boxSize + _margin, _boxSize + _margin, 1);
            index++;
        }

        // A zero alpha is culled before it costs a draw - see ColoredPlane.Color - so the
        // pool's leftovers just stay queued and invisible.
        for (var i = lit; i < _highlights.Count; i++) _highlights[i].Color = Vector4.Zero;
    }

    /// <summary>The slot's box and why it is lit, or null when it is not lit at all.</summary>
    private (float X, float Y, bool Selected, bool Hovered)? Lit(int index)
    {
        var hovered = _hoverRun is { } run && index >= run.Start && index <= run.End;
        var selected = IsSelected?.Invoke(index) ?? false;
        if (!hovered && !selected) return null;

        return BoxAt(index) is { } box ? (box.X, box.Y, selected, hovered) : null;
    }

    /// <summary>
    ///     The slots the hovered one lights: its whole <see cref="GroupOf" /> group, which is
    ///     always a contiguous run - the expansion emits an item's sounds back to back.
    /// </summary>
    private (int Start, int End)? HoverRun()
    {
        if (_hovered is not { } index) return null;
        if (GroupOf?.Invoke(index) is not { } group) return (index, index);

        var start = index;
        var end = index;
        while (start > 0 && ReferenceEquals(GroupOf(start - 1), group)) start--;
        while (end + 1 < _events.Length && ReferenceEquals(GroupOf(end + 1), group)) end++;
        return (start, end);
    }

    private ColoredPlane PlaneAt(int index)
    {
        while (_highlights.Count <= index)
        {
            var created = new ColoredPlane();
            _highlights.Add(created);
            // Index 0 of this canvas's layer: the panels have to paint under the icons, and
            // a plane created after DrawSelf has run would otherwise queue behind them.
            if (Drawn) Context.QueueRender(created, Index, 0);
        }

        return _highlights[index];
    }

    /// <summary>The slot's box in absolute coordinates, or null when it is out of range.</summary>
    private (float X, float Y)? BoxAt(int index)
    {
        return RenderableAt(index) is { } renderable ? BoxOf(renderable) : null;
    }

    /// <summary>
    ///     A slot's box in absolute coordinates. A renderable's own position is its texture's,
    ///     centred in the box by the aspect correction, so the box is recovered from the two.
    /// </summary>
    private (float X, float Y) BoxOf(SoundRenderable renderable)
    {
        var (px, py, _) = renderable.Position;
        var (sx, sy, _) = renderable.Scale;
        return (px - (_boxSize - sx) / 2, py - (_boxSize - sy) / 2);
    }

    /// <summary>
    ///     Tracks the slot under the pointer. Clicks carry coordinates but hovering does not,
    ///     so the box is resolved per frame - the canvas is one element, not one per slot.
    /// </summary>
    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        RefreshCulling();

        var hovered = HighlightHover && IsHovered ? IndexAt(Context.PointerX, Context.PointerY) : null;
        if (hovered == _hovered) return;

        _hovered = hovered;
        RefreshSelection();
    }

    /// <summary>
    ///     Keeps only the chunks that reach the visible rectangle queued for rendering.
    ///     A queued chunk is drawn every frame and updates every renderable it holds, so an
    ///     imported cover's worth of them costs a frame whether it is on screen or not - which
    ///     is the whole of the FPS drop a long sequence used to cause.
    ///     A chunk is 512 events, so this is coarse on purpose: the point is that the cost
    ///     follows the panel's height rather than the sequence's length.
    /// </summary>
    private void RefreshCulling()
    {
        var next = VisibleChunks();
        for (var i = next.First; i <= next.Last; i++) CatchUp(i);
        if (next == _queued) return;

        // Dequeue what left the window before queueing what entered it: the two ranges
        // usually overlap, and a chunk in both must not be dropped after being re-added.
        for (var i = _queued.First; i <= _queued.Last; i++)
            if (i < next.First || i > next.Last)
                Context.DequeueRender(_chunks[i], Index);

        for (var i = next.First; i <= next.Last; i++)
            if (i < _queued.First || i > _queued.Last)
                Context.QueueRender(_chunks[i], Index);

        _queued = next;
    }

    /// <summary>
    ///     The events the queued chunks hold, as an inclusive index range - everything that
    ///     can be on screen. (0, -1) when nothing is.
    /// </summary>
    private (int First, int Last) VisibleEvents()
    {
        if (_queued.Last < _queued.First) return (0, -1);

        var first = 0;
        for (var i = 0; i < _queued.First; i++) first += _chunks[i].Renderables.Length;

        var last = first - 1;
        for (var i = _queued.First; i <= _queued.Last; i++) last += _chunks[i].Renderables.Length;

        return (first, Math.Min(last, _events.Length - 1));
    }

    /// <summary>
    ///     The inclusive range of chunks reaching the visible rectangle, or (0, -1) for none.
    ///     Without a clip (nothing is scissoring this canvas) every chunk counts.
    /// </summary>
    private (int First, int Last) VisibleChunks()
    {
        if (!Drawn || _chunks.Count == 0) return (0, -1);
        if (_clip is not { } clip) return (0, _chunks.Count - 1);

        var first = -1;
        var last = -2;
        for (var i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            // A bounce lifts a sound above its chunk's StartY, so the window is grown by one
            // line - otherwise the top row's hop is culled away mid-animation.
            if (chunk.EndY + LineHeight < clip.Y || chunk.StartY - LineHeight > clip.W) continue;
            if (first < 0) first = i;
            last = i;
        }

        return first < 0 ? (0, -1) : (first, last);
    }

    /// <summary>
    ///     Plays the playfield's bounce on one slot - what the visualizer does as it plays it.
    ///     <paramref name="scale" /> is its height against that: a fraction hops less far (a
    ///     previewed sound), a negative one dips (a value scrolled down).
    /// </summary>
    public void Bounce(int index, float scale = 1f)
    {
        RenderableAt(index)?.Bounce(scale);
    }

    /// <summary>
    ///     What the playfield does to an action as it passes it - the fade and expand of
    ///     <c>PlayfieldContainer.NormalSubscription</c>. Sounds bounce, actions do this.
    /// </summary>
    public void FadeExpand(int index)
    {
        if (RenderableAt(index) is not { } renderable) return;
        renderable.Fade();
        renderable.Expand();
    }

    /// <summary>
    ///     Rewrites the number on a slot - a "!loopmany" counting its passes down, a "!stop"
    ///     counting its wait out - and fades and expands it, as the visualizer's
    ///     LoopMany/Stop handlers do. Only slots that drew a number in the first place have
    ///     the text buffer to rewrite; see RenderableFactory.AssignTextBuffers.
    /// </summary>
    public void SetValue(int index, BaseEvent ev, ValueChangeWrapMode mode)
    {
        if (RenderableAt(index) is { Value: NormalText } renderable) renderable.SetValue(ev, mode);
    }

    /// <summary>Whether the slot is mid-bounce; a playing slot ignores value gestures.</summary>
    public bool IsBouncing(int index)
    {
        return RenderableAt(index)?.IsBouncing ?? false;
    }

    /// <summary>
    ///     One drawn line's height - the box this canvas currently draws at plus its margin.
    ///     Not <see cref="FaithfulSizing.SoundSize" />: a fitted canvas shrinks the box to get
    ///     its count across, so the constant is only right at full width.
    /// </summary>
    public float LineHeight => _boxSize + _margin;

    /// <summary>The slot's top edge, relative to this canvas's own origin. Null when out of range.</summary>
    public float? OffsetOf(int index)
    {
        return BoxAt(index) is { } box ? box.Y - Computed.AbsoluteY : null;
    }

    private SoundRenderable? RenderableAt(int index)
    {
        for (var c = 0; c < _chunks.Count; c++)
        {
            var chunk = _chunks[c];
            if (index < chunk.Renderables.Length)
            {
                // Off-screen chunks lag the scroll (see CatchUp), and this is read for slots
                // that need not be on screen - a drag reaching past the end, a bounce.
                CatchUp(c);
                return chunk.Renderables[index];
            }

            index -= chunk.Renderables.Length;
        }

        return null;
    }

    /// <summary>
    ///     A press on a slot captures the pointer, so the drag frames come here rather than
    ///     scrolling the view. Only when a move handler is wired - the palettes have none.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        if (OnMove is null) return false;
        _dragIndex = IndexAt(x, y);
        _dragGroup = _dragIndex is { } index ? GroupOf?.Invoke(index) : null;
        _dragOver = _dragGroup ?? _dragIndex;
        _dragged = false;
        return _dragIndex is not null;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_dragIndex is not { } from || DragTarget(x, y) is not { } to || to == from) return;

        var over = GroupOf?.Invoke(to) ?? to;
        if (Equals(over, _dragOver)) return;

        _dragged = true;
        _dragOver = over;
        OnMove?.Invoke(from, to);

        // Where the dragged thing actually went, not where the pointer is: a move re-lays
        // the whole block out, and an item several slots wide (a "!combine" group) that
        // swapped past it now covers the cell under the pointer. Measuring the next
        // boundary from there would carry on dragging that item instead of this one.
        _dragIndex = SlotOf(_dragGroup) ?? to;
    }

    /// <summary>The first slot belonging to a group, or null - see <see cref="GroupOf" />.</summary>
    private int? SlotOf(object? group)
    {
        if (group is null || GroupOf is null) return null;

        for (var index = 0; index < _events.Length; index++)
            if (ReferenceEquals(GroupOf(index), group))
                return index;

        return null;
    }

    /// <summary>
    ///     Right-press is level-triggered (fires every held frame) and previewing is not
    ///     idempotent, so it acts once per press - same guard as SoundPicker's duplicate.
    /// </summary>
    public override bool HandleRightPress(float x, float y)
    {
        if (OnPreview is null) return false;
        if (Context.RightPressId == _handledRightPressId) return true;
        _handledRightPressId = Context.RightPressId;

        if (IndexAt(x, y) is { } index) OnPreview.Invoke(index);
        return true;
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        if (OnAdjust is null) return false;
        var notches = MathF.Sign(scrollDelta.Y);
        if (notches == 0) return false;
        if (IndexAt(Context.PointerX, Context.PointerY) is not { } index) return false;

        OnAdjust.Invoke(index, notches);
        return true;
    }

    /// <summary>
    ///     Walks the layout without moving a single renderable: where each chunk sits, where
    ///     each one starts from, and where the block ends. That is everything culling and the
    ///     element's own height need - the chunks are laid out for real when they come on
    ///     screen, by <see cref="CatchUp" />.
    ///     The walk itself is arithmetic; it is writing a position, a model matrix and three
    ///     text placements per slot that costs, and on an imported cover that is a quarter of
    ///     a second for slots nobody is looking at.
    /// </summary>
    private void MeasureAll()
    {
        if (_seek.Length != _chunks.Count)
        {
            _seek = new (int, float, float)[_chunks.Count];
            _stale = new bool[_chunks.Count];
        }

        var layout = _generator!.LayoutHandler;
        layout.Reset();

        var right = Computed.AbsoluteX;
        var bottom = Computed.AbsoluteY;

        for (var i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            _seek[i] = (layout.CurrentSoundIndex, layout.Y, layout.Height);
            _stale[i] = true;
            chunk.StartY = layout.Y;

            foreach (var renderable in chunk.Renderables)
            {
                // The same box GetNewPosition hands PositionSound, so this is the extent the
                // old whole-block measure arrived at - see BoxOf.
                var (x, y) = layout.GetNewPosition(renderable.IsDivider);
                right = Math.Max(right, x + _boxSize + _margin / 2);
                bottom = Math.Max(bottom, y + _boxSize + _margin / 2);
            }

            chunk.EndY = layout.Height + layout.Size;
        }

        _extent = (right, bottom);
    }

    /// <summary>
    ///     Moves the block without touching a renderable: a scroll shifts everything by one
    ///     number, so the bounds and the recorded seek states take it and the chunks are left
    ///     to <see cref="CatchUp" />.
    /// </summary>
    private void ShiftBy(float dy)
    {
        if (dy == 0) return;

        for (var i = 0; i < _chunks.Count; i++)
        {
            _chunks[i].StartY += dy;
            _chunks[i].EndY += dy;
            _seek[i].Y += dy;
            _seek[i].Height += dy;
            _stale[i] = true;
        }

        _extent.Bottom += dy;
    }

    /// <summary>Brings one chunk's renderables up to where the scrolls since have put it.</summary>
    private void CatchUp(int index)
    {
        if (index < 0 || index >= _stale.Length || !_stale[index]) return;

        // The bounds this writes back are the ones ShiftBy already worked out, so the chunk
        // lands exactly where culling has been treating it as being.
        _generator!.PositionChunk(_chunks[index], _seek[index]);
        _stale[index] = false;
    }

    protected override void DrawSelf(UIContext context)
    {
        // Highlights first: within a layer, render order follows queue order, so these paint
        // under the icons they sit behind.
        foreach (var plane in _highlights) context.QueueRender(plane, Index, 0);

        // Only what is on screen - see RefreshCulling. Queued from scratch here because a
        // draw pass may follow a rebuild that dropped every chunk this range named.
        _queued = (0, -1);
        RefreshCulling();
    }

    public override void StopRendering()
    {
        foreach (var plane in _highlights) Context.DequeueRender(plane, Index);
        foreach (var chunk in _chunks) Context.DequeueRender(chunk, Index);
        _queued = (0, -1);
        base.StopRendering();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _clip = clip;
        foreach (var plane in _highlights) plane.ClipRect = clip;
        foreach (var chunk in _chunks) chunk.ClipRect = clip;
        base.ApplyClip(clip);
    }
}
