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
    public const int MaxValue = 60; // the TDW default value range
    public const int Rows = MaxValue * 2 + 1;
    public const float GutterWidth = 44f;
    public const float StripHeight = 22f;
    public const float RulerHeight = 18f;
    public const float GridTop = StripHeight + RulerHeight;

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
    private static readonly Vector4 GutterColor = new(0.086f, 0.086f, 0.118f, 1f); // #16161e
    private static readonly Vector4 StripColor = new(0.086f, 0.086f, 0.118f, 1f);
    private static readonly Vector4 StripSegmentA = new(0.16f, 0.18f, 0.26f, 1f); // #292e42
    private static readonly Vector4 StripSegmentB = new(0.21f, 0.23f, 0.33f, 1f);
    private static readonly Vector4 StripSelected = new(0.30f, 0.42f, 0.80f, 1f); // #4c6bcc
    private static readonly Vector4 StepLineColor = new(0.11f, 0.12f, 0.17f, 1f);
    private static readonly Vector4 BeatLineColor = new(0.16f, 0.18f, 0.26f, 1f);
    private static readonly Vector4 RowLineColor = new(0.10f, 0.11f, 0.15f, 1f);
    private static readonly Vector4 OctaveLineColor = new(0.20f, 0.22f, 0.31f, 1f);
    private static readonly Vector4 BoundaryColor = new(0.34f, 0.37f, 0.54f, 1f); // #565f89
    private static readonly Vector4 ZeroRowColor = new(0.10f, 0.11f, 0.16f, 1f);
    private static readonly Vector4 SelectedNoteColor = new(0.61f, 0.75f, 1f, 1f); // #9bc0ff
    private static readonly Vector4 LabelColor = new(0.66f, 0.7f, 0.86f, 1f);
    private static readonly Vector4 PlayheadColor = new(0.75f, 0.79f, 0.96f, 1f); // #c0caf5

    // Stable per-sound colors (string.GetHashCode is randomized per process).
    private static readonly Vector4[] SoundPalette =
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

    internal readonly List<Panel> AutomationMarks = [];
    private readonly List<NoteBlock> _noteBlocks = [];
    private readonly LineBatch _lineBatch = new();
    private readonly EditorState _state;
    private readonly List<StripBlock> _stripBlocks = [];
    internal readonly List<Label> BeatLabels = [];
    private readonly List<Label> _gutterLabels = [];
    private readonly List<Panel> _playheads = [];
    private readonly List<float> _playheadXs = [];
    private readonly Panel _zeroRow;
    private readonly Panel _gutterBackground;
    private readonly Panel _stripBackground;
    private readonly Panel _rulerBackground;

    private NoteBlock? _dragging;
    private (TrackSegment segment, Note note)? _placing;
    private Vector4i? _inheritedClip;
    private float _rowHeight = 8f;
    private float _scrollX;
    private float _scrollY;
    private bool _centerPending = true; // a fresh view (and every OpenTrack) starts centered on value 0
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
        for (var i = 0; i < AutomationMarkPool; i++)
        {
            var mark = NewGhost(context, StepLineColor);
            AutomationMarks.Add(mark);
            AddChild(mark);
        }

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
    public float PixelsPerStep { get; set; } = 64f;

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
        _rowHeight = Math.Max(RowHeight, (height - GridTop) / Rows);
        // Stays pending until the user scrolls: flex parents lay this view out more
        // than once per frame with different heights, and only the last one counts.
        if (_centerPending) _scrollY = (Rows * _rowHeight - (height - GridTop)) / 2;

        ClampScroll();
        var pps = PixelsPerStep;
        var visibleStart = _scrollX;
        var visibleEnd = _scrollX + Math.Max(0, width - GutterWidth);
        CollectPlayheadXs(track, width, pps);

        // The grid only spans the track's segments — everything past the last one is
        // dead space where clicks can't place, so it must not look placeable.
        var contentPx = (track?.Segments.Sum(s => s.StepCount) ?? 0) * pps;
        var gridWidth = Math.Clamp(contentPx - _scrollX, 0, Math.Max(0, width - GutterWidth));

        _zeroRow.X = GutterWidth;
        _zeroRow.Y = ValueTop(0);
        _zeroRow.Width = gridWidth;
        _zeroRow.Height = _rowHeight;

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        for (var r = 0; r < Rows + 1; r++)
        {
            var y = GridTop + r * _rowHeight - _scrollY;
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
                PlaceNote(dragged, dragged.Segment, dragged.Note, SegmentStartPx(track, dragged.Segment));

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
                    block.X = GutterWidth + segStart - _scrollX;
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
                    var x = GutterWidth + segStart - _scrollX;
                    _lineBatch.Set(BoundaryLineSlot + boundary++, absX + x, absY + GridTop, 1, height - GridTop,
                        BoundaryColor);
                }

                var firstLocal = Math.Max(0, (int)Math.Floor((visibleStart - segStart) / pps));
                var lastLocal = Math.Min(segment.StepCount - 1, (int)Math.Ceiling((visibleEnd - segStart) / pps));
                for (var s = firstLocal; s <= lastLocal && stepLine < StepLinePool; s++)
                {
                    if (s == 0) continue; // the boundary line already marks the segment start
                    var x = GutterWidth + segStart + s * pps - _scrollX;
                    var color = s % segment.StepsPerBeat == 0 ? BeatLineColor : StepLineColor;
                    _lineBatch.Set(StepLineSlot + stepLine++, absX + x, absY + GridTop, 1, height - GridTop, color);
                }

                // The beat ruler labels every beat boundary the step-line loop above just
                // colored — MinBeatLabelSpacingPx thins them at low zoom so they never overlap.
                var firstBeatLocal = firstLocal - firstLocal % segment.StepsPerBeat;
                for (var s = firstBeatLocal; s <= lastLocal && beatLabel < BeatLabels.Count; s += segment.StepsPerBeat)
                {
                    var bx = GutterWidth + segStart + s * pps - _scrollX;
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
                        DrawAutomation(track, segment, note, segStart, ref autoMark);
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
        for (var i = autoMark; i < AutomationMarks.Count; i++) Hide(AutomationMarks[i]);
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
            var y = ValueTop(value) + (_rowHeight - 11f) / 2;
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

            var x = GutterWidth + (float)(track.StepPositionAt(localMinutes) * pps) - _scrollX;
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
        var left = _scrollX + margin;
        var right = _scrollX + viewportWidth - margin;

        if (playheadPx < left)
        {
            _scrollX = Math.Max(0, playheadPx - margin);
            InvalidateLayout();
        }
        else if (playheadPx > right)
        {
            _scrollX = playheadPx - viewportWidth + margin;
            InvalidateLayout();
        }
    }

    /// <summary>
    ///     Plots a note's generated automation events as a step path in the note's sound
    ///     color: a horizontal run at the current value, a vertical jump where a keyframe
    ///     changes it, and a short tick at every generated event (so pure repeats stay
    ///     visible as "a horizontal line with small vertical lines"). Time-mode gaps are
    ///     mapped through the note's own segment step rate — display-only approximation
    ///     when the path crosses into a segment with another tempo.
    /// </summary>
    private void DrawAutomation(ProjectTrack track, TrackSegment segment, Note note, float segStartPx, ref int used)
    {
        var stepMinutes = segment.StepMinutes(track.Timing.BPM);
        if (stepMinutes <= 0) return;

        var color = InstrumentColor(note.Instrument);
        var prevX = GutterWidth + segStartPx + (note.Step + 0.5f) * PixelsPerStep - _scrollX;
        var prevY = ValueTop(Math.Clamp(note.Value, -MaxValue, MaxValue)) + _rowHeight / 2;

        foreach (var (minutes, generated) in note.Automation!.ExpandNotes(note, 0, stepMinutes))
        {
            var x = GutterWidth + segStartPx +
                    (note.Step + 0.5f + (float)(minutes / stepMinutes)) * PixelsPerStep - _scrollX;
            var y = ValueTop(Math.Clamp(generated.Value, -MaxValue, MaxValue)) + _rowHeight / 2;

            // The horizontal run, the value jump (only when the value moved), the tick.
            if (!Mark(ref used, Math.Min(prevX, x), prevY - 0.5f, Math.Abs(x - prevX), 1f, color)) return;
            if (Math.Abs(y - prevY) >= 1f &&
                !Mark(ref used, x - 0.5f, Math.Min(prevY, y), 1f, Math.Abs(y - prevY), color)) return;
            if (!Mark(ref used, x - 1f, y - _rowHeight * 0.3f, 2f, _rowHeight * 0.6f, color)) return;

            prevX = x;
            prevY = y;
        }
    }

    private bool Mark(ref int used, float x, float y, float width, float height, Vector4 color)
    {
        if (used >= AutomationMarks.Count) return false; // pool cap: the path just ends early
        var mark = AutomationMarks[used++];
        mark.X = x;
        mark.Y = y;
        mark.Width = width;
        mark.Height = height;
        ((ColoredPlane)mark.Background!).Color = color;
        return true;
    }

    private void PlaceNote(NoteBlock block, TrackSegment segment, Note note, float segStartPx)
    {
        block.Assign(segment, note);
        block.X = GutterWidth + segStartPx + note.Step * PixelsPerStep - _scrollX;
        block.Y = ValueTop(note.Value) + 0.5f;
        block.Width = Math.Max(3, PixelsPerStep - 1);
        block.Height = Math.Max(3, _rowHeight - 1);
        ((ColoredPlane)block.Background!).Color =
            note == _state.SelectedNote ? SelectedNoteColor : InstrumentColor(note.Instrument);
    }

    private float ValueTop(double value)
    {
        return GridTop + (float)((MaxValue - value) * _rowHeight) - _scrollY;
    }

    private static void Hide(UIElement element)
    {
        element.Width = 0;
        element.Height = 0;
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        _centerPending = false;
        if (WheelZooms)
        {
            // Zoom anchored at the pointer: the step under the cursor stays put.
            var pointerPx = Context.PointerX - Computed.AbsoluteX - GutterWidth;
            var anchorSteps = (pointerPx + _scrollX) / PixelsPerStep;
            PixelsPerStep = Math.Clamp(PixelsPerStep * MathF.Pow(1.15f, scrollDelta.Y), 4f, 128f);
            _scrollX = anchorSteps * PixelsPerStep - pointerPx;
        }
        else if (FineSnap)
        {
            // FL bindings: Shift+wheel pans time.
            _scrollX -= scrollDelta.Y * 48f;
        }
        else
        {
            // Plain wheel scrolls the value rows; a tilt wheel / touchpad X pans time.
            _scrollY -= scrollDelta.Y * 48f;
            _scrollX -= scrollDelta.X * 48f;
        }

        ClampScroll();
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
            _centerPending = false;
            _scrollX += last.X - x;
            _scrollY += last.Y - y;
            _panPointer = new Vector2(x, y);
            ClampScroll();
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
        _centerPending = true;
        InvalidateLayout();
    }

    private void ClampScroll()
    {
        var track = _state.OpenedTrack;
        var total = track?.Segments.Sum(s => s.StepCount) * PixelsPerStep ?? 0;
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, total - (Computed.Width - GutterWidth)));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, Rows * _rowHeight - (Computed.Height - GridTop)));
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

        var steps = Math.Max(0, (absX - Computed.AbsoluteX - GutterWidth + _scrollX) / PixelsPerStep);
        var localMinutes = track.MinutesAtStepPosition(steps);

        var placements = _state.Project.Placements.Where(p => p.Track == track).ToArray();
        if (placements.Length == 0) return;
        var placement = placements.Length == 1
            ? placements[0]
            : placements.MinBy(p => Math.Abs(p.StartQuarterNotes - PlayheadQuarters))!;

        OnSeekQuarters.Invoke(placement.StartQuarterNotes + localMinutes * _state.Project.RootTiming.BPM);
    }

    /// <summary>Maps an absolute x to (segment, local step); null outside the track's grid.</summary>
    private (TrackSegment? segment, int step) StepAt(float absX, bool clamp)
    {
        var track = _state.OpenedTrack;
        if (track == null) return (null, 0);

        var step = (int)Math.Floor((absX - Computed.AbsoluteX - GutterWidth + _scrollX) / PixelsPerStep);
        if (step < 0)
        {
            if (!clamp) return (null, 0);
            step = 0;
        }

        TrackSegment? last = null;
        var lastStep = 0;
        foreach (var segment in track.Segments)
        {
            if (segment.StepCount <= 0) continue;
            if (step < segment.StepCount) return (segment, step);
            step -= segment.StepCount;
            last = segment;
            lastStep = segment.StepCount - 1;
        }

        return clamp && last != null ? (last, lastStep) : (null, 0);
    }

    private double ValueAt(float absY)
    {
        var r = (absY - Computed.AbsoluteY - GridTop + _scrollY) / _rowHeight;
        var value = FineSnap
            ? Math.Round((MaxValue - (r - 0.5)) * 5) / 5
            : MaxValue - Math.Floor(r);
        return Math.Clamp(value, -MaxValue, MaxValue);
    }

    private float SegmentStartPx(ProjectTrack track, TrackSegment target)
    {
        float offset = 0;
        foreach (var segment in track.Segments)
        {
            if (segment == target) break;
            offset += segment.StepCount * PixelsPerStep;
        }

        return offset;
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

    /// <summary>A purely visual overlay: never takes pointer input.</summary>
    private class GhostPanel(UIContext context) : Panel(context)
    {
        public override UIElement? HitTest(float x, float y)
        {
            return null;
        }
    }

    private class StripBlock : Panel
    {
        public StripBlock(UIContext context, TrackEditorView view) : base(context)
        {
            Width = 0;
            Height = 0;
            Background = new ColoredPlane { Color = StripSegmentA };
            OnClick = _ =>
            {
                if (Segment != null) view._state.SelectSegment(Segment);
            };
        }

        public TrackSegment? Segment { get; set; }
    }

    private class NoteBlock : Panel
    {
        private readonly TrackEditorView _view;

        public NoteBlock(UIContext context, TrackEditorView view) : base(context)
        {
            _view = view;
            Width = 0;
            Height = 0;
            Background = new ColoredPlane { Color = SoundPalette[0] };
            // Swallow the click so a release on a note never bubbles into the view's
            // place-at-pointer handler; selection already happened on press.
            OnClick = _ => { };
        }

        public TrackSegment? Segment { get; private set; }
        public Note? Note { get; private set; }

        public void Assign(TrackSegment segment, Note note)
        {
            Segment = segment;
            Note = note;
        }

        public override bool HandlePress(float x, float y)
        {
            if (Note == null || Segment == null) return false;
            _view._dragging = this;
            _view._state.BeginGesture();
            _view._state.SelectSegment(Segment);
            _view._state.SelectNote(Note);
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
            var (segment, step) = _view.StepAt(x, true);
            if (segment == null) return;

            var value = _view.ValueAt(y);
            var moved = segment != Segment || step != Note.Step || value != Note.Value;
            _view._state.MoveNote(Segment, segment, Note, step, value);
            Segment = segment;
            _view.InvalidateLayout();
            // Re-preview only on an actual cell change, replacing the old preview.
            if (moved) _view.OnPreviewNote?.Invoke(Note.Instrument, Note.Value);
        }
    }
}
