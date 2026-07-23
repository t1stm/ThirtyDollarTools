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
///     The per-track note editor (FL piano-roll style, absolute values). Horizontal =
///     grid steps, segments laid back to back at a constant width per step (integer
///     <see cref="Note.Step" /> math, no snapping logic); vertical = <see cref="Note.Value" />
///     in semitones, +24 on top, 0 centered. The value gutter labels become note names
///     once the sound-value→note map exists. Placement and drags snap values to integers,
///     or to 0.2 while <see cref="FineSnap" /> (Shift) is held; existing fractional values
///     render at their exact y. A segment strip on top selects segments; the wheel pans.
///     Everything renders from fixed pools reassigned in DoLayout, so the Aleph-0 track
///     sizes (thousands of segments/notes) only ever pay for the visible range — and a
///     drag never loses its captured element to a rebuild. A beat ruler between the strip
///     and the grid labels the running beat number at every beat boundary, highlighting
///     whichever beat the playhead is currently in.
/// </summary>
public sealed class TrackEditorView : Panel
{
    public const int MaxValue = TrackEditorGeometry.MaxValue;
    public const int Rows = TrackEditorGeometry.Rows;
    public const float GutterWidth = TrackEditorGeometry.GutterWidth;
    public const float StripHeight = TrackEditorGeometry.StripHeight;
    public const float RulerHeight = TrackEditorGeometry.RulerHeight;
    public const float GridTop = TrackEditorGeometry.GridTop;

    // ponytail: fixed pools sized for a ~3k px window at minimum zoom (4 px/step) and
    // BMS-dense charts (~10 notes per visible step at default zoom). If a chart still
    // exhausts the note pool, newly appended notes are the first dropped — the upgrade
    // path is a reserved block for the selected note, not a bigger pool.
    private const int StepLinePool = 768;
    private const int NoteBlockPool = 2048;
    private const int StripBlockPool = 512; // changed from 256 for Aleph-0 demo
    private const int BoundaryLinePool = 512; // changed from 256 for Aleph-0 demo
    private const int AutomationMarkPool = 768; // ≤3 marks per generated automation event

    // Slot ranges within _lineBatch: row lines, then step lines, then boundary lines.
    private const int RowLineSlot = 0;
    private const int StepLineSlot = RowLineSlot + Rows + 1;
    private const int BoundaryLineSlot = StepLineSlot + StepLinePool;
    private const int LineBatchTotal = BoundaryLineSlot + BoundaryLinePool;

    // ponytail: worst case (StepsPerBeat=1 at 4px/step min zoom) would need ~750 like
    // StepLinePool, but MinBeatLabelSpacingPx bounds real usage to ~110 labels for a
    // 3000px viewport, so 128 is generous without paying for a 768-sized label pool.
    private const int BeatLabelPool = 128;
    private const float MinBeatLabelSpacingPx = 28f;

    private static readonly Vector4 BackgroundColor = new(0.067f, 0.07f, 0.1f, 1f); // #11121a
    private static readonly Vector4 GutterColor = EditorPalette.Panel;
    private static readonly Vector4 StripColor = EditorPalette.Panel;
    internal static readonly Vector4 StripSegmentA = EditorPalette.Surface;
    private static readonly Vector4 StripSegmentB = EditorPalette.SurfaceRaised;
    private static readonly Vector4 StripSelected = EditorPalette.Accent;
    private static readonly Vector4 StepLineColor = new(0.11f, 0.12f, 0.17f, 1f);
    private static readonly Vector4 BeatLineColor = EditorPalette.Surface;
    private static readonly Vector4 RowLineColor = new(0.10f, 0.11f, 0.15f, 1f);
    private static readonly Vector4 OctaveLineColor = new(0.20f, 0.22f, 0.31f, 1f);
    private static readonly Vector4 BoundaryColor = EditorPalette.TextMuted;
    private static readonly Vector4 ZeroRowColor = new(0.10f, 0.11f, 0.16f, 1f);
    private static readonly Vector4 SelectedNoteColor = EditorPalette.SelectionHighlight;
    private static readonly Vector4 LabelColor = EditorPalette.TextDim;
    private static readonly Vector4 PlayheadColor = EditorPalette.Playhead;

    // Stable per-sound colors (string.GetHashCode is randomized per process).
    internal static readonly Vector4[] SoundPalette =
    [
        new(0.30f, 0.42f, 0.80f, 1f), // blue
        new(0.62f, 0.36f, 0.71f, 1f), // purple
        new(0.24f, 0.60f, 0.46f, 1f), // green
        new(0.78f, 0.47f, 0.25f, 1f), // orange
        new(0.72f, 0.32f, 0.42f, 1f), // rose
        new(0.28f, 0.56f, 0.67f, 1f), // teal
        new(0.66f, 0.58f, 0.28f, 1f), // olive
        new(0.48f, 0.44f, 0.78f, 1f) // violet
    ];

    private readonly AutomationPath _automationPath;
    private readonly List<NoteBlock> _noteBlocks = [];
    private readonly LineBatch _lineBatch = new();
    internal readonly EditorState _state;
    private readonly TrackEditorGeometry _geometry = new();
    private readonly List<StripBlock> _stripBlocks = [];
    internal readonly List<Label> BeatLabels = [];
    internal IReadOnlyList<Panel> AutomationMarks => _automationPath.Marks;
    private readonly List<Label> _gutterLabels = [];
    private readonly List<Panel> _playheads = [];
    private readonly List<float> _playheadXs = [];
    private readonly Panel _zeroRow;
    private readonly Panel _gutterBackground;
    private readonly Panel _stripBackground;
    private readonly Panel _rulerBackground;

    internal NoteBlock? _dragging;
    private (TrackSegment segment, Note note)? _placing;
    private Vector4i? _inheritedClip;
    private Vector2? _panPointer;

    public TrackEditorView(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        Focusable = true;
        Background = new ColoredPlane { Color = BackgroundColor };

        // Row/step/boundary lines render as one instanced draw call (see LineBatch)
        // queued in DrawSelf, below Children in the same depth layer — same spot in
        // paint order "grid furniture first" put them in before.
        _lineBatch.Count = LineBatchTotal;

        _zeroRow = NewGhost(context, ZeroRowColor);
        AddChild(_zeroRow);

        // Automation paths render under the note blocks and never take input.
        _automationPath = new AutomationPath(context, this, AutomationMarkPool, StepLineColor);

        for (var i = 0; i < NoteBlockPool; i++)
        {
            var block = new NoteBlock(context, this);
            _noteBlocks.Add(block);
            AddChild(block);
        }

        var strip = NewGhost(context, StripColor);
        AddChild(strip);
        _stripBackground = strip;
        for (var i = 0; i < StripBlockPool; i++)
        {
            var block = new StripBlock(context, this);
            _stripBlocks.Add(block);
            AddChild(block);
        }

        var ruler = NewGhost(context, StripColor);
        AddChild(ruler);
        _rulerBackground = ruler;
        for (var i = 0; i < BeatLabelPool; i++)
        {
            var label = new Label(context, "1") { FontSizePx = 11f, Color = LabelColor };
            BeatLabels.Add(label);
            AddChild(label);
        }

        var gutter = new Panel(context)
        {
            Background = new ColoredPlane { Color = GutterColor },
            OnClick = _ => { } // swallow: never place notes through the gutter
        };
        _gutterBackground = gutter;
        AddChild(gutter);
        for (var value = MaxValue; value >= -MaxValue; value--)
        {
            var label = new Label(context, value switch
            {
                > 0 => $"+{value}",
                < 0 => $"{value}",
                _ => $" {value}"
            })
            {
                FontSizePx = 11f,
                Color = LabelColor
            };
            _gutterLabels.Add(label);
            AddChild(label);
        }
    }

    /// <summary>Horizontal zoom: pixels per grid step. Ctrl+wheel adjusts it (4–128).</summary>
    public float PixelsPerStep
    {
        get => _geometry.PixelsPerStep;
        set => _geometry.PixelsPerStep = value;
    }

    /// <summary>
    ///     Minimum height of one value row. Small viewports scroll vertically instead of
    ///     compressing the rows; viewports taller than 49 rows stretch them to fill.
    /// </summary>
    public float RowHeight { get; set; } = 20f;

    /// <summary>While true (Shift held), value placement/drag snaps to 0.2 instead of 1.</summary>
    public bool FineSnap { get; set; }

    /// <summary>While true (Ctrl held), the wheel zooms horizontally instead of panning.</summary>
    public bool WheelZooms { get; set; }

    /// <summary>Fired when a note is placed or moved, with its instrument and value — the preview seam.</summary>
    public Action<Instrument, double>? OnPreviewNote { get; set; }

    /// <summary>Fired with the clicked arrangement-timeline position (quarter notes) when the beat ruler is clicked.</summary>
    public Action<double>? OnSeekQuarters { get; set; }

    /// <summary>
    ///     Playback position on the arrangement timeline, in quarter notes at the root BPM
    ///     (same value the arrangement view's playhead uses). The opened track can be
    ///     placed on the arrangement more than once, so this draws one playhead line per
    ///     placement currently inside its window — none while nothing is playing.
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

    /// <summary>
    /// When enabled, the editor automatically scrolls horizontally to keep the
    /// playhead visible during playback.
    /// </summary>
    public bool FollowPlayhead { get; set; } = true;

    protected override void DoLayout()
    {
        var track = _state.OpenedTrack;
        var width = Computed.Width;
        var height = Computed.Height;
        _geometry.SetViewport(width, height, RowHeight);
        _geometry.ClampScroll();

        var pps = PixelsPerStep;
        var scrollX = _geometry.ScrollX;
        var scrollY = _geometry.ScrollY;
        var rowHeight = _geometry.RowHeight;
        var visibleStart = scrollX;
        var visibleEnd = scrollX + Math.Max(0, width - GutterWidth);
        CollectPlayheadXs(track, width, pps);

        // The grid only spans the track's segments — everything past the last one is
        // dead space where clicks can't place, so it must not look placeable.
        var contentPx = (track?.Segments.Sum(s => s.StepCount) ?? 0) * pps;
        var gridWidth = Math.Clamp(contentPx - scrollX, 0, Math.Max(0, width - GutterWidth));

        _zeroRow.X = GutterWidth;
        _zeroRow.Y = _geometry.ValueTop(0);
        _zeroRow.Width = gridWidth;
        _zeroRow.Height = rowHeight;

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        for (var r = 0; r < Rows + 1; r++)
        {
            var y = GridTop + r * rowHeight - scrollY;
            var visibleWidth = y >= 0 && y <= height ? gridWidth : 0;
            var color = (MaxValue - r) % 12 == 0 ? OctaveLineColor : RowLineColor;
            _lineBatch.Set(RowLineSlot + r, absX + GutterWidth, absY + y, visibleWidth, 1, color);
        }

        var stepLine = 0;
        var noteBlock = 0;
        var stripBlock = 0;
        var boundary = 0;
        var autoMark = 0;
        var beatLabel = 0;
        var lastLabelX = float.NegativeInfinity;

        if (track != null)
        {
            // One pass over the segments covers the strip, the boundary/step lines and
            // the notes; segments fully outside the visible range cost one addition.
            if (_dragging is { Note: not null, Segment: not null } dragged)
                // The dragged note is pinned to its block so pool reassignment (and
                // scrolling) can never steal the captured element mid-drag.
                PlaceNote(dragged, dragged.Segment, dragged.Note, _geometry.SegmentStartPx(track, dragged.Segment));

            float offset = 0;
            var beatAccum = 0;
            for (var i = 0; i < track.Segments.Count && offset <= visibleEnd; i++)
            {
                var segment = track.Segments[i];
                var segWidth = segment.StepCount * pps;
                var segStart = offset;
                var segBeatStart = beatAccum;
                offset += segWidth;
                beatAccum += segment.Bars * segment.Numerator;
                if (offset < visibleStart || segWidth <= 0) continue;

                if (stripBlock < _stripBlocks.Count)
                {
                    var block = _stripBlocks[stripBlock++];
                    block.Segment = segment;
                    block.X = GutterWidth + segStart - scrollX;
                    block.Y = 2;
                    block.Width = segWidth - 1;
                    block.Height = StripHeight - 4;
                    ((ColoredPlane)block.Background!).Color = segment == _state.SelectedSegment
                        ? StripSelected
                        : i % 2 == 0
                            ? StripSegmentA
                            : StripSegmentB;
                }

                if (boundary < BoundaryLinePool && segStart >= visibleStart)
                {
                    var x = GutterWidth + segStart - scrollX;
                    _lineBatch.Set(BoundaryLineSlot + boundary++, absX + x, absY + GridTop, 1, height - GridTop,
                        BoundaryColor);
                }

                var firstLocal = Math.Max(0, (int)Math.Floor((visibleStart - segStart) / pps));
                var lastLocal = Math.Min(segment.StepCount - 1, (int)Math.Ceiling((visibleEnd - segStart) / pps));
                for (var s = firstLocal; s <= lastLocal && stepLine < StepLinePool; s++)
                {
                    if (s == 0) continue; // the boundary line already marks the segment start
                    var x = GutterWidth + segStart + s * pps - scrollX;
                    var color = s % segment.StepsPerBeat == 0 ? BeatLineColor : StepLineColor;
                    _lineBatch.Set(StepLineSlot + stepLine++, absX + x, absY + GridTop, 1, height - GridTop, color);
                }

                // The beat ruler labels every beat boundary the step-line loop above just
                // colored — MinBeatLabelSpacingPx thins them at low zoom so they never overlap.
                var firstBeatLocal = firstLocal - firstLocal % segment.StepsPerBeat;
                for (var s = firstBeatLocal; s <= lastLocal && beatLabel < BeatLabels.Count; s += segment.StepsPerBeat)
                {
                    var bx = GutterWidth + segStart + s * pps - scrollX;
                    if (bx - lastLabelX < MinBeatLabelSpacingPx) continue;
                    lastLabelX = bx;

                    var beatWidthPx = segment.StepsPerBeat * pps;
                    var isCurrent = _playheadXs.Exists(px => px >= bx && px < bx + beatWidthPx);
                    var label = BeatLabels[beatLabel++];
                    label.Color = isCurrent ? PlayheadColor : LabelColor;
                    label.SetTextContents($"{segBeatStart + s / segment.StepsPerBeat + 1}");
                    label.X = bx + 2;
                    label.Y = StripHeight + (RulerHeight - 11f) / 2;
                }

                foreach (var note in segment.Notes)
                {
                    if (note.Automation != null && segStart + note.Step * pps <= visibleEnd)
                        _automationPath.Draw(_geometry, track, segment, note, segStart,
                            InstrumentColor(note.Instrument), ref autoMark);
                    if (_dragging?.Note == note) continue;
                    var x = segStart + note.Step * pps;
                    if (x + pps < visibleStart || x > visibleEnd) continue;
                    while (noteBlock < _noteBlocks.Count && _noteBlocks[noteBlock] == _dragging) noteBlock++;
                    if (noteBlock >= _noteBlocks.Count) break;
                    PlaceNote(_noteBlocks[noteBlock++], segment, note, segStart);
                }
            }
        }

        for (var i = noteBlock; i < _noteBlocks.Count; i++)
            if (_noteBlocks[i] != _dragging)
                Hide(_noteBlocks[i]);
        _automationPath.HideUnused(autoMark);
        for (var i = stepLine; i < StepLinePool; i++)
            _lineBatch.Set(StepLineSlot + i, 0, 0, 0, 0, StepLineColor);
        for (var i = stripBlock; i < _stripBlocks.Count; i++) Hide(_stripBlocks[i]);
        for (var i = boundary; i < BoundaryLinePool; i++)
            _lineBatch.Set(BoundaryLineSlot + i, 0, 0, 0, 0, BoundaryColor);
        // Labels don't gate their own draw on Width/Height, so releasing a slot means
        // parking it outside the view's clip instead of zeroing size like Hide() does.
        for (var i = beatLabel; i < BeatLabels.Count; i++) BeatLabels[i].X = -1000f;

        _stripBackground.X = 0;
        _stripBackground.Y = 0;
        _stripBackground.Width = width;
        _stripBackground.Height = StripHeight;

        _rulerBackground.X = 0;
        _rulerBackground.Y = StripHeight;
        _rulerBackground.Width = width;
        _rulerBackground.Height = RulerHeight;

        _gutterBackground.X = 0;
        _gutterBackground.Y = 0;
        _gutterBackground.Width = GutterWidth;
        _gutterBackground.Height = height;

        for (var i = 0; i < _gutterLabels.Count; i++)
        {
            var value = MaxValue - i;
            var y = _geometry.ValueTop(value) + (rowHeight - 11f) / 2;
            // Labels render above the strip background, so scrolled-out ones must be
            // parked outside the view's clip instead of relying on paint order.
            _gutterLabels[i].X = y < GridTop || y + 11f > height ? -1000f : 8;
            _gutterLabels[i].Y = y;
        }

        LayoutPlayheads(height);

        base.DoLayout();
        ApplyClip(_inheritedClip);
    }

    /// <summary>
    ///     Computes the on-screen x of every visible playhead (a track placed more than once
    ///     on the arrangement can have several — one per placement whose local playback
    ///     window currently contains the playhead), auto-scrolling to follow it during
    ///     playback. Shared by the playhead lines and the beat ruler's current-beat highlight.
    /// </summary>
    private void CollectPlayheadXs(ProjectTrack? track, float width, float pps)
    {
        _playheadXs.Clear();
        if (track == null || PlayheadQuarters <= double.NegativeInfinity) return;

        var bpm = _state.Project.RootTiming.BPM;
        var duration = track.DurationMinutes();
        foreach (var placement in _state.Project.Placements)
        {
            if (placement.Track != track) continue;

            var localMinutes = (PlayheadQuarters - placement.StartQuarterNotes) / bpm;
            if (localMinutes < 0 || localMinutes >= duration) continue;

            if (_state.IsCurrentlyPlayingAudio && FollowPlayhead)
                HandlePlayheadScrollUpdate(track, width, pps, localMinutes);

            var x = GutterWidth + (float)(track.StepPositionAt(localMinutes) * pps) - _geometry.ScrollX;
            if (x < GutterWidth || x >= width) continue;

            _playheadXs.Add(x);
        }
    }

    /// <summary>One line per entry in <see cref="_playheadXs" />, computed earlier in DoLayout.</summary>
    private void LayoutPlayheads(float height)
    {
        var shown = 0;
        foreach (var x in _playheadXs)
        {
            if (shown >= _playheads.Count)
            {
                var created = NewGhost(Context, PlayheadColor);
                _playheads.Add(created);
                AddChild(created);
            }

            var line = _playheads[shown++];
            line.X = x;
            line.Y = GridTop;
            line.Width = 2;
            line.Height = height - GridTop;
        }

        for (var i = shown; i < _playheads.Count; i++) Hide(_playheads[i]);
    }

    private void HandlePlayheadScrollUpdate(ProjectTrack track, float width, float pps, double localMinutes)
    {
        var playheadPx = (float)(track.StepPositionAt(localMinutes) * pps);

        var viewportWidth = Math.Max(0, width - GutterWidth);

        var margin = viewportWidth / 2;
        var left = _geometry.ScrollX + margin;
        var right = _geometry.ScrollX + viewportWidth - margin;

        if (playheadPx < left)
        {
            _geometry.ScrollX = Math.Max(0, playheadPx - margin);
            InvalidateLayout();
        }
        else if (playheadPx > right)
        {
            _geometry.ScrollX = playheadPx - viewportWidth + margin;
            InvalidateLayout();
        }
    }


    private void PlaceNote(NoteBlock block, TrackSegment segment, Note note, float segStartPx)
    {
        block.Assign(segment, note);
        block.X = GutterWidth + segStartPx + note.Step * PixelsPerStep - _geometry.ScrollX;
        block.Y = _geometry.ValueTop(note.Value) + 0.5f;
        block.Width = Math.Max(3, PixelsPerStep - 1);
        block.Height = Math.Max(3, _geometry.RowHeight - 1);
        ((ColoredPlane)block.Background!).Color =
            note == _state.SelectedNote ? SelectedNoteColor : InstrumentColor(note.Instrument);
    }

    private static void Hide(UIElement element)
    {
        element.Width = 0;
        element.Height = 0;
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        _geometry.CenterPending = false;
        if (WheelZooms)
        {
            // Zoom anchored at the pointer: the step under the cursor stays put.
            var pointerPx = Context.PointerX - Computed.AbsoluteX - GutterWidth;
            _geometry.ZoomAt(pointerPx, scrollDelta.Y);
        }
        else if (FineSnap)
        {
            // FL bindings: Shift+wheel pans time.
            _geometry.ScrollX -= scrollDelta.Y * 48f;
        }
        else
        {
            // Plain wheel scrolls the value rows; a tilt wheel / touchpad X pans time.
            _geometry.ScrollY -= scrollDelta.Y * 48f;
            _geometry.ScrollX -= scrollDelta.X * 48f;
        }

        _geometry.ClampScroll();
        InvalidateLayout();
        return true;
    }

    /// <summary>
    ///     FL-style middle-mouse pan of both axes, fed per frame from the scene's mouse
    ///     handler (the framework only routes left/right buttons). A hold that starts
    ///     inside the view drags the viewport with the pointer until release.
    /// </summary>
    public void MiddlePan(bool held, float x, float y)
    {
        if (!held)
        {
            _panPointer = null;
            return;
        }

        if (_panPointer is { } last)
        {
            _geometry.CenterPending = false;
            _geometry.ScrollX += last.X - x;
            _geometry.ScrollY += last.Y - y;
            _panPointer = new Vector2(x, y);
            _geometry.ClampScroll();
            InvalidateLayout();
        }
        else if (ContainsPoint(x, y))
        {
            _panPointer = new Vector2(x, y);
        }
    }

    /// <summary>Scrolls so value 0 sits mid-viewport on the next layout.</summary>
    public void CenterOnZero()
    {
        _geometry.CenterPending = true;
        InvalidateLayout();
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Escape:
                _state.CloseTrack();
                return true;
            case Keys.Delete or Keys.Backspace when _state is { SelectedNote: { } note, OpenedTrack: { } track }:
                var segment = track.Segments.FirstOrDefault(s => s.Notes.Contains(note));
                if (segment != null) _state.RemoveNote(segment, note);
                return true;
            default:
                return base.HandleKeyDown(e);
        }
    }

    public override UIElement? HitTest(float x, float y)
    {
        // Pooled children are positioned directly; scrolled-out ones must not take clicks.
        return !Visible || !ContainsPoint(x, y) ? null : base.HitTest(x, y);
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _inheritedClip = clip;
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var own = IntersectClip(new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height), clip);

        foreach (var child in Children) child.ApplyClip(own);
        if (Background != null) Background.ClipRect = clip;
        _lineBatch.ClipRect = own;
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
        if (_placing != null && uiContext.CapturedElement != this) _placing = null;
        if (_dragging == null || uiContext.CapturedElement == _dragging) return;

        _dragging = null; // drag ended: let the pool reassign freely again
        InvalidateLayout();
    }

    /// <summary>
    ///     Pressing an empty cell places the active sound immediately (FL paint style)
    ///     and captures the pointer: holding the button and sweeping paints a note into
    ///     every new cell crossed. Presses on existing notes never reach here — their
    ///     blocks win the hit-test (and dragging those moves them instead).
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        var localX = x - Computed.AbsoluteX;
        var localY = y - Computed.AbsoluteY;
        if (localY is >= StripHeight and < GridTop)
        {
            SeekToPointer(x);
            return true;
        }
        if (localY < GridTop || localX < GutterWidth) return false;

        var (segment, step) = StepAt(x, false);
        if (_state.ActiveInstrument is not { } instrument || segment == null)
        {
            _state.SelectNote(null);
            return false;
        }

        if (Paint(segment, step, instrument, ValueAt(y)) is not { } painted) return false;
        _placing = painted;
        return true; // capture: the sweep keeps painting
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_placing is not ({ } lastSegment, { } lastNote)) return;
        var (segment, step) = StepAt(x, true);
        if (segment == null) return;

        var value = ValueAt(y);
        if (segment == lastSegment && step == lastNote.Step && value == lastNote.Value) return; // same cell

        if (Paint(segment, step, lastNote.Instrument, value) is { } painted) _placing = painted;
    }

    /// <summary>Places one painted note, unless the cell already holds an identical one.</summary>
    private (TrackSegment segment, Note note)? Paint(TrackSegment segment, int step, Instrument instrument, double value)
    {
        if (segment.Notes.Any(n => n.Step == step && n.Value == value)) return null;

        var note = _state.AddNote(segment, step, instrument, value);
        _state.SelectSegment(segment);
        _state.SelectNote(note);
        OnPreviewNote?.Invoke(instrument, note.Value);
        InvalidateLayout();
        return (segment, note);
    }

    /// <summary>
    ///     Converts a ruler click's absolute x into an arrangement-timeline seek: the track can
    ///     be placed on the arrangement more than once, so this seeks the occurrence nearest the
    ///     current playhead rather than guessing "first".
    /// </summary>
    private void SeekToPointer(float absX)
    {
        if (_state.OpenedTrack is not { } track || OnSeekQuarters == null) return;

        var steps = Math.Max(0, (absX - Computed.AbsoluteX - GutterWidth + _geometry.ScrollX) / PixelsPerStep);
        var localMinutes = track.MinutesAtStepPosition(steps);

        var placements = _state.Project.Placements.Where(p => p.Track == track).ToArray();
        if (placements.Length == 0) return;
        var placement = placements.Length == 1
            ? placements[0]
            : placements.MinBy(p => Math.Abs(p.StartQuarterNotes - PlayheadQuarters))!;

        OnSeekQuarters.Invoke(placement.StartQuarterNotes + localMinutes * _state.Project.RootTiming.BPM);
    }

    /// <summary>Maps an absolute x to (segment, local step); null outside the track's grid.</summary>
    internal (TrackSegment? segment, int step) StepAt(float absX, bool clamp)
    {
        return _geometry.StepAt(_state.OpenedTrack, absX - Computed.AbsoluteX, clamp);
    }

    internal double ValueAt(float absY)
    {
        return _geometry.ValueAt(absY - Computed.AbsoluteY, FineSnap);
    }

    private static Vector4 InstrumentColor(Instrument instrument)
    {
        var hash = 0;
        foreach (var c in instrument.Name) hash = hash * 31 + c;
        return SoundPalette[(hash & 0x7fffffff) % SoundPalette.Length];
    }

    private static Panel NewGhost(UIContext context, Vector4 color)
    {
        return new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Background = new ColoredPlane { Color = color }
        };
    }

}
