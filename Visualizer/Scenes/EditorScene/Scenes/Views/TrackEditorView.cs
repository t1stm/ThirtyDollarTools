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
///     The per-track note editor (FL piano-roll style, absolute values). Horizontal =
///     grid steps, segments laid back to back at a constant width per step (integer
///     <see cref="Note.Step" /> math, no snapping logic); vertical = <see cref="Note.Value" />
///     in semitones, +24 on top, 0 centered. The value gutter labels become note names
///     once the sound-value→note map exists. Placement and drags snap values to integers,
///     or to 0.2 while <see cref="FineSnap" /> (Shift) is held; existing fractional values
///     render at their exact y. A segment strip on top selects segments; the wheel pans.
///     Everything renders from fixed pools reassigned in DoLayout, so the Aleph-0 track
///     sizes (thousands of segments/notes) only ever pay for the visible range - and a
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
    public const float CutRowHeight = TrackEditorGeometry.CutRowHeight;
    public const float GridTop = TrackEditorGeometry.GridTop;

    // ponytail: fixed pools sized for a ~3k px window at minimum zoom (4 px/step) and
    // BMS-dense charts (~10 notes per visible step at default zoom). If a chart still
    // exhausts the note pool, newly appended notes are the first dropped - the upgrade
    // path is a reserved block for the selected note, not a bigger pool.
    private const int StepLinePool = 768;
    private const int NoteBlockPool = 2048;
    private const int StripBlockPool = 512; // changed from 256 for Aleph-0 demo
    private const int BoundaryLinePool = 512; // changed from 256 for Aleph-0 demo
    private const int AutomationMarkPool = 768; // ≤3 marks per generated automation event

    // Slot ranges within _lineBatch: the cut row's background first (so every later
    // slot paints over it - see the pinned cut row's paint-order comment below), then
    // row lines, step lines, then boundary lines.
    private const int CutRowBgSlot = 0;
    private const int RowLineSlot = CutRowBgSlot + 1;
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
    private static readonly Vector4 CutRowColor = EditorPalette.SurfaceRaised;
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

    internal readonly List<Label> BeatLabels = [];
    internal readonly List<Label> GutterLabels = [];

    private readonly AutomationPath _automationPath;
    private readonly Label _cutRowLabel;
    private readonly Panel _cutRule;
    private readonly TrackEditorGeometry _geometry = new();
    private readonly Panel _gutterBackground;
    private readonly LineBatch _lineBatch = new();
    private readonly Panel _marqueeRect;
    private readonly List<NoteBlock> _noteBlocks = [];
    private readonly List<float> _playheadXs = [];
    private readonly List<Panel> _playheads = [];
    private readonly Panel _rulerBackground;
    internal readonly EditorState _state;
    private readonly Panel _stripBackground;
    private readonly List<StripBlock> _stripBlocks = [];
    private readonly Panel _zeroRow;

    internal NoteBlock? _dragging;
    private List<GroupDragEntry>? _groupDrag;
    private int _groupDragAnchorStartStep;
    private double _groupDragAnchorStartValue;
    private int _groupDragLastStep;
    private double _groupDragLastValue;
    private Vector4i? _inheritedClip;
    private (double Step, double Value)? _marqueeAnchor;
    private (double Step, double Value)? _marqueeCursor;
    private MarqueeMode _marqueeMode;
    private (TrackSegment segment, Note note)? _placing;

    /// <summary>Last pointer y of a Ctrl+middle row-scaling drag; null while none is running.</summary>
    private float? _rowScaleY;

    public TrackEditorView(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        Focusable = true;
        Background = new ColoredPlane { Color = BackgroundColor };

        // Row/step/boundary lines render as one instanced draw call (see LineBatch)
        // queued in DrawSelf, below Children in the same depth layer - same spot in
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
            GutterLabels.Add(label);
            AddChild(label);
        }

        _cutRowLabel = new Label(context, "!cut") { FontSizePx = 11f, Color = LabelColor };
        AddChild(_cutRowLabel);

        // Added after the gutter and its label so the rule spans the full width,
        // crossing the gutter column too, instead of being cut off by it.
        _cutRule = NewGhost(context, BoundaryColor);
        AddChild(_cutRule);

        // Added last so it renders above every note block (same trick ArrangementView
        // uses for its playhead); never takes input (GhostPanel), fill only, no border.
        _marqueeRect = new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Background = new ColoredPlane
                { Color = new Vector4(EditorPalette.Accent.X, EditorPalette.Accent.Y, EditorPalette.Accent.Z, 0.25f) }
        };
        AddChild(_marqueeRect);
    }

    internal IReadOnlyList<Panel> AutomationMarks => _automationPath.Marks;
    internal IReadOnlyList<NoteBlock> NoteBlocks => _noteBlocks;

    /// <summary>Horizontal zoom: pixels per grid step. Ctrl+wheel adjusts it (4–128).</summary>
    public float PixelsPerStep
    {
        get => _geometry.PixelsPerStep;
        set => _geometry.PixelsPerStep = value;
    }

    /// <summary>
    ///     Height of one value row, scaled by a Ctrl+middle drag between
    ///     <see cref="TrackEditorGeometry.MinRowHeight" /> and
    ///     <see cref="TrackEditorGeometry.MaxRowHeight" />. The grid always scrolls
    ///     vertically rather than stretching to fill the viewport.
    /// </summary>
    public float RowHeight
    {
        get => _geometry.RowHeight;
        set
        {
            _geometry.RowHeight = value;
            InvalidateLayout();
        }
    }

    /// <summary>While true (Shift held), value placement/drag snaps to 0.2 instead of 1.</summary>
    public bool FineSnap { get; set; }

    /// <summary>While true (Ctrl held), the wheel zooms horizontally instead of panning.</summary>
    public bool WheelZooms { get; set; }

    /// <summary>Fired when a note is placed or moved, with its instrument and value - the preview seam.</summary>
    public Action<Instrument, double>? OnPreviewNote { get; set; }

    /// <summary>Fired with the clicked arrangement-timeline position (quarter notes) when the beat ruler is clicked.</summary>
    public Action<double>? OnSeekQuarters { get; set; }

    /// <summary>
    ///     Playback position on the arrangement timeline, in quarter notes at the root BPM
    ///     (same value the arrangement view's playhead uses). The opened track can be
    ///     placed on the arrangement more than once, so this draws one playhead line per
    ///     placement currently inside its window - none while nothing is playing.
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
    ///     When enabled, the editor automatically scrolls horizontally to keep the
    ///     playhead visible during playback.
    /// </summary>
    public bool FollowPlayhead { get; set; } = true;

    protected override void DoLayout()
    {
        var track = _state.OpenedTrack;
        var width = Computed.Width;
        var height = Computed.Height;
        _geometry.SetViewport(width, height);
        _geometry.ClampScroll();

        var pps = PixelsPerStep;
        // Auto-scroll (following the playhead) mutates _geometry.ScrollX live - run it
        // before scrollX is snapshotted below, so grid lines (using the snapshot) and
        // PlaceNote (reading _geometry.ScrollX directly) never land a frame apart.
        CollectPlayheadXs(track, width, pps);

        var cutRowTop = _geometry.CutRowTop;
        var gridBottom = _geometry.GridBottom;
        var scrollX = _geometry.ScrollX;
        var scrollY = _geometry.ScrollY;
        var rowHeight = _geometry.RowHeight;
        var visibleStart = scrollX;
        var visibleEnd = scrollX + Math.Max(0, width - GutterWidth);

        // The grid only spans the track's segments - everything past the last one is
        // dead space where clicks can't place, so it must not look placeable.
        var contentPx = (track?.Segments.Sum(s => s.StepCount) ?? 0) * pps;
        var gridWidth = Math.Clamp(contentPx - scrollX, 0, Math.Max(0, width - GutterWidth));

        var zeroRowY = _geometry.ValueTop(0);
        _zeroRow.X = GutterWidth;
        _zeroRow.Y = zeroRowY;
        _zeroRow.Width = gridWidth;
        _zeroRow.Height = Math.Max(0, Math.Min(zeroRowY + rowHeight, gridBottom) - zeroRowY);

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        _lineBatch.Set(CutRowBgSlot, absX, absY + cutRowTop, width, CutRowHeight, CutRowColor);

        for (var r = 0; r < Rows + 1; r++)
        {
            var y = GridTop + r * rowHeight - scrollY;
            var visibleWidth = y >= GridTop && y + 1 <= gridBottom ? gridWidth : 0;
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
                // colored - MinBeatLabelSpacingPx thins them at low zoom so they never overlap.
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

                // Individual cuts routinely fire several sounds at once ("!cut@a|!cut@b"),
                // landing several cut notes (different instruments) on the same step - group
                // them so the render loop below can split that step into equal slots instead
                // of stacking every one of them at identical coordinates in the pinned row.
                Dictionary<int, List<Note>>? cutsByStep = null;
                foreach (var n in segment.Notes)
                {
                    if (!n.IsCut) continue;
                    cutsByStep ??= new Dictionary<int, List<Note>>();
                    if (!cutsByStep.TryGetValue(n.Step, out var list))
                        cutsByStep[n.Step] = list = [];
                    list.Add(n);
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

                    if (note.IsCut && cutsByStep![note.Step] is { Count: > 1 } siblings)
                        PlaceNote(_noteBlocks[noteBlock++], segment, note, segStart, siblings.IndexOf(note),
                            siblings.Count);
                    else
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

        // The labels are 11px tall: below a 10px row they collide, so only every Nth
        // value keeps its label, spaced ~12px apart (every 3rd at the 4px floor).
        // Striding on the value, not the index, keeps 0 labelled at every zoom.
        var labelStride = rowHeight >= 10f ? 1 : (int)MathF.Ceiling(12f / rowHeight);

        for (var i = 0; i < GutterLabels.Count; i++)
        {
            var value = MaxValue - i;
            var y = _geometry.ValueTop(value) + (rowHeight - 11f) / 2;
            // Labels render above the strip background, so scrolled-out (and skipped)
            // ones must be parked outside the view's clip instead of relying on paint order.
            var skipped = value % labelStride != 0;
            GutterLabels[i].X = skipped || y < GridTop || y + 11f > gridBottom ? -1000f : 8;
            GutterLabels[i].Y = y;
        }

        // Fixed position - always visible, never parked outside the clip like the
        // scrollable gutter labels above.
        _cutRowLabel.X = 8;
        _cutRowLabel.Y = cutRowTop + (CutRowHeight - 11f) / 2;

        // Full-width rule separating the grid from the pinned cut row, crossing the
        // gutter column too (added after it in the constructor so it paints on top).
        _cutRule.X = 0;
        _cutRule.Y = gridBottom;
        _cutRule.Width = width;
        _cutRule.Height = TrackEditorGeometry.RuleHeight;

        LayoutPlayheads(height);
        LayoutMarquee(pps, scrollX, gridBottom);

        base.DoLayout();
        ApplyClip(_inheritedClip);
    }

    /// <summary>
    ///     Positions the marquee rectangle from its model-space anchor/cursor every
    ///     frame, so it scroll-corrects; zero size (hidden) while no marquee is active.
    /// </summary>
    private void LayoutMarquee(float pps, float scrollX, float gridBottom)
    {
        if (_marqueeAnchor is not { } anchor || _marqueeCursor is not { } cursor)
        {
            _marqueeRect.Width = 0;
            _marqueeRect.Height = 0;
            return;
        }

        var x1 = GutterWidth + (float)(anchor.Step * pps) - scrollX;
        var x2 = GutterWidth + (float)(cursor.Step * pps) - scrollX;
        var y1 = _geometry.ValueTop(anchor.Value);
        var y2 = _geometry.ValueTop(cursor.Value);

        var top = Math.Min(y1, y2);
        var bottom = Math.Min(Math.Max(y1, y2), gridBottom);

        _marqueeRect.X = Math.Min(x1, x2);
        _marqueeRect.Y = top;
        _marqueeRect.Width = Math.Abs(x2 - x1);
        _marqueeRect.Height = Math.Max(0, bottom - top);
    }

    /// <summary>
    ///     Computes the on-screen x of every visible playhead (a track placed more than once
    ///     on the arrangement can have several - one per placement whose local playback
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


    private void PlaceNote(NoteBlock block, TrackSegment segment, Note note, float segStartPx,
        int cutSlot = 0, int cutSlotCount = 1)
    {
        block.Assign(segment, note);
        var stepX = GutterWidth + segStartPx + note.Step * PixelsPerStep - _geometry.ScrollX;
        if (note.IsCut)
        {
            // Fixed row - never scrolls, unlike every other row. Simultaneous cuts on
            // different instruments (cutSlotCount > 1) split the step evenly instead of
            // stacking exactly on top of each other.
            var slotWidth = PixelsPerStep / cutSlotCount;
            block.X = stepX + cutSlot * slotWidth;
            block.Y = _geometry.CutRowTop + 0.5f;
            block.Width = Math.Max(3, slotWidth - 1);
            block.Height = Math.Max(3, CutRowHeight - 1);
        }
        else
        {
            var y = _geometry.ValueTop(note.Value) + 0.5f;
            block.X = stepX;
            block.Y = y;
            block.Width = Math.Max(3, PixelsPerStep - 1);
            // No overdraw covers a grid note bleeding past the grid's bottom edge
            // (unlike the top, still covered by the strip/ruler children), so clamp it.
            var naturalHeight = Math.Max(3, _geometry.RowHeight - 1);
            block.Height = Math.Max(0, Math.Min(y + naturalHeight, _geometry.GridBottom) - y);
        }

        ((ColoredPlane)block.Background!).Color =
            _state.SelectedNotes.Contains(note) ? SelectedNoteColor : InstrumentColor(note.Instrument);
    }

    private static void Hide(UIElement element)
    {
        element.Width = 0;
        element.Height = 0;
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        _geometry.CenterPending = false;
        var pointerPx = Context.PointerX - Computed.AbsoluteX - GutterWidth;
        _geometry.Nav.Wheel(scrollDelta, WheelZooms, FineSnap, pointerPx);
        InvalidateLayout();
        return true;
    }

    /// <summary>
    ///     FL-style middle-mouse pan of both axes, fed per frame from the scene's mouse
    ///     handler (the framework only routes left/right buttons). A hold that starts
    ///     inside the view drags the viewport with the pointer until release. With Ctrl
    ///     held, the same drag scales the row height instead of panning.
    /// </summary>
    public void MiddlePan(bool held, float x, float y)
    {
        // ponytail: WheelZooms is just "Ctrl is held" - rename it if it starts lying louder.
        if (held && WheelZooms)
        {
            _geometry.Nav.MiddlePan(false, x, y, false); // cancel any pan already in progress
            if (_rowScaleY is { } last) _geometry.ScaleRows(y - Computed.AbsoluteY, y - last);
            else if (!ContainsPoint(x, y)) return;

            _rowScaleY = y;
            _geometry.CenterPending = false;
            InvalidateLayout();
            return;
        }

        _rowScaleY = null;
        if (!_geometry.Nav.MiddlePan(held, x, y, ContainsPoint(x, y))) return;
        _geometry.CenterPending = false;
        InvalidateLayout();
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
            // Escape clears the selection first; only once nothing is selected does it
            // fall through to closing the track (see Editor.KeyDown for the same chain
            // when this view isn't the focused element).
            case Keys.Escape when _state.SelectedNotes.Count > 0:
                _state.ClearSelection();
                return true;
            case Keys.Escape:
                _state.CloseTrack();
                return true;
            case Keys.Delete or Keys.Backspace when _state.SelectedNotes.Count > 0:
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

        if (_marqueeAnchor != null && uiContext.CapturedElement != this)
        {
            CommitMarquee();
            _marqueeAnchor = _marqueeCursor = null;
            InvalidateLayout();
        }

        if (_dragging == null || uiContext.CapturedElement == _dragging) return;

        _dragging = null; // drag ended: let the pool reassign freely again
        _groupDrag = null;
        InvalidateLayout();
    }

    /// <summary>
    ///     Pressing an empty cell places the active sound immediately (FL paint style)
    ///     and captures the pointer: holding the button and sweeping paints a note into
    ///     every new cell crossed. Presses on existing notes never reach here - their
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

        if (localX < GutterWidth) return false;
        if (localY >= _geometry.CutRowTop) return HandleCutRowPress(x);
        if (localY < GridTop) return false;

        if (_state.ActiveTool == EditorTool.Select)
        {
            _marqueeAnchor = _marqueeCursor = (UnsnappedStepAt(x), UnsnappedValueAt(y));
            _marqueeMode = FineSnap ? MarqueeMode.Remove : WheelZooms ? MarqueeMode.Append : MarqueeMode.Replace;
            InvalidateLayout();
            return true; // capture: the drag updates the marquee rect
        }

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

    /// <summary>
    ///     A press in the pinned cut row places/starts sweeping a cut targeting whichever
    ///     instrument is active - any instrument can be cut, there's no reserved one.
    /// </summary>
    private bool HandleCutRowPress(float x)
    {
        var (segment, step) = StepAt(x, false);
        if (_state.ActiveInstrument is not { } instrument || segment == null) return false;

        if (Paint(segment, step, instrument, 0, true) is not { } painted) return false;
        _placing = painted;
        return true; // capture: the sweep keeps painting
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_marqueeAnchor != null)
        {
            _marqueeCursor = (UnsnappedStepAt(x), UnsnappedValueAt(y));
            InvalidateLayout();
            return;
        }

        if (_placing is not ({ } lastSegment, { } lastNote)) return;
        var (segment, step) = StepAt(x, true);
        if (segment == null) return;

        if (lastNote.IsCut)
        {
            if (segment == lastSegment && step == lastNote.Step) return; // same cell
            if (Paint(segment, step, lastNote.Instrument, 0, true) is { } cutPainted) _placing = cutPainted;
            return;
        }

        var value = ValueAt(y);
        if (segment == lastSegment && step == lastNote.Step && value == lastNote.Value) return; // same cell

        if (Paint(segment, step, lastNote.Instrument, value) is { } painted) _placing = painted;
    }

    /// <summary>
    ///     Applies the marquee's Replace/Append/Remove modifier semantics against every note
    ///     whose (global step, value) falls inside the box - the note itself, not its
    ///     rendered block, so pooled-out notes at chart-dense zoom are never missed.
    /// </summary>
    private void CommitMarquee()
    {
        if (_state.OpenedTrack is not { } track) return;
        if (_marqueeAnchor is not { } anchor || _marqueeCursor is not { } cursor) return;

        var minStep = Math.Min(anchor.Step, cursor.Step);
        var maxStep = Math.Max(anchor.Step, cursor.Step);
        var minValue = Math.Min(anchor.Value, cursor.Value);
        var maxValue = Math.Max(anchor.Value, cursor.Value);

        var contained = new List<Note>();
        var offset = 0;
        foreach (var segment in track.Segments)
        {
            foreach (var note in segment.Notes)
            {
                // Cut notes render in the pinned row, outside the scrollable grid the
                // marquee draws in - never reachable by it, regardless of their (unused) Value.
                if (note.IsCut) continue;

                // A note isn't a point - it's a whole rendered cell, one step wide
                // (globalStep .. globalStep+1) and one row tall. The row is drawn
                // top-anchored (ValueTop), so its cell spans (Value-1 .. Value], the
                // opposite orientation from the step axis. Comparing the marquee's
                // box against the note's exact (step, value) point (rather than this
                // cell) only ever matched when the box touched the cell's leading
                // edge - top-only, for the value axis - missing anything dragged
                // entirely inside the cell.
                var globalStep = offset + note.Step;
                var stepOverlaps = globalStep < maxStep && globalStep + 1 > minStep;
                var valueOverlaps = note.Value - 1 < maxValue && note.Value > minValue;
                if (stepOverlaps && valueOverlaps) contained.Add(note);
            }

            offset += segment.StepCount;
        }

        switch (_marqueeMode)
        {
            case MarqueeMode.Append:
                _state.AddToNoteSelection(contained);
                break;
            case MarqueeMode.Remove:
                _state.RemoveFromNoteSelection(contained);
                break;
            default:
                _state.SetNoteSelection(contained); // empty marquee clears the selection
                break;
        }
    }

    /// <summary>
    ///     Starts a group drag on the given note block: captures every currently
    ///     selected note's starting (global step, value) - the block's own note is one
    ///     of them (a fresh press onto an unselected note already replaced the selection
    ///     with just it, so this naturally degrades to a plain single-note drag).
    /// </summary>
    internal void BeginNoteDrag(NoteBlock block, float pressY)
    {
        if (_state.OpenedTrack is not { } track || block.Note == null || block.Segment == null) return;

        _dragging = block;
        _state.BeginGesture();

        var anchorGlobalStep = track.GlobalStepOf(block.Segment, block.Note.Step);
        _groupDragAnchorStartStep = anchorGlobalStep;
        // Snapped pointer value at press, not the note's own exact Value: a fractional value
        // (e.g. 6.4) only snaps back to itself in FineSnap mode, so anchoring on the exact
        // value would read every later frame's ValueAt(y) as a nonzero delta even at the same
        // pointer position - including the same-position "drag" frame the input dispatcher
        // fires for a plain held click (see UIContext.UpdatePointer) - silently rounding the
        // note on a click, and losing the fraction on an intentional drag (delta = snapped -
        // exact instead of snapped - snapped). Anchoring on the snap makes a same-position
        // frame's delta exactly zero, and a one-row drag's delta a clean whole number that
        // adds onto each note's own exact StartValue below - preserving the fraction.
        _groupDragAnchorStartValue = ValueAt(pressY);
        _groupDragLastStep = anchorGlobalStep;
        _groupDragLastValue = _groupDragAnchorStartValue;

        _groupDrag = _state.SelectedNotes.Select(note =>
        {
            var segment = track.Segments.FirstOrDefault(s => s.Notes.Contains(note));
            var globalStep = segment != null ? track.GlobalStepOf(segment, note.Step) : note.Step;
            return new GroupDragEntry(note, globalStep, note.Value);
        }).ToList();
    }

    /// <summary>
    ///     Applies the anchor's per-frame delta (from its own drag start) to every
    ///     selected note's own start, so the whole group moves together - a group of one
    ///     reduces to exactly the old single-note drag. Steps/values are clamped into the
    ///     track's valid range per note (matching FL: notes at the edge just stop there,
    ///     which can compress the group's relative spacing at the boundary - acceptable).
    /// </summary>
    internal void UpdateGroupDrag(float x, float y)
    {
        if (_groupDrag is not { Count: > 0 } entries || _state.OpenedTrack is not { } track) return;

        var (segment, step) = StepAt(x, true);
        if (segment == null) return;
        var value = ValueAt(y);

        var newAnchorGlobalStep = track.GlobalStepOf(segment, step);
        var stepDelta = newAnchorGlobalStep - _groupDragAnchorStartStep;
        var valueDelta = value - _groupDragAnchorStartValue;

        var maxGlobalStep = Math.Max(0, track.Segments.Sum(s => s.StepCount) - 1);
        var targets = new List<(Note Note, TrackSegment Segment, int Step, double Value)>(entries.Count);
        foreach (var entry in entries)
        {
            var targetGlobalStep = Math.Clamp(entry.StartGlobalStep + stepDelta, 0, maxGlobalStep);
            // A cut note's Value is unused (it renders in the fixed pinned row regardless),
            // so clamping it along with everything else is harmless - no special case needed.
            var targetValue = Math.Clamp(entry.StartValue + valueDelta, -MaxValue, MaxValue);
            if (track.SegmentAtGlobalStep(targetGlobalStep) is not { } mapped) continue;

            targets.Add((entry.Note, mapped.Segment, mapped.LocalStep, targetValue));
        }

        _state.MoveSelectedNotes(track, targets);

        // The pinned block's own Segment must track its note across a boundary crossing.
        foreach (var target in targets)
            if (target.Note == _dragging?.Note)
            {
                _dragging!.Segment = target.Segment;
                break;
            }

        var moved = newAnchorGlobalStep != _groupDragLastStep || value != _groupDragLastValue;
        _groupDragLastStep = newAnchorGlobalStep;
        _groupDragLastValue = value;
        InvalidateLayout();
        if (moved) OnPreviewNote?.Invoke(_dragging!.Note!.Instrument, value);
    }

    /// <summary>
    ///     Places one painted note, unless the cell already holds an identical one. A cut's
    ///     dedup is per (step, instrument) - independent instruments' cuts may share a step,
    ///     but re-pressing the same instrument's cut on the same step is a no-op, not a stack.
    /// </summary>
    private (TrackSegment segment, Note note)? Paint(TrackSegment segment, int step, Instrument instrument,
        double value, bool isCut = false)
    {
        var duplicate = isCut
            ? segment.Notes.Any(n => n.Step == step && n.IsCut && n.Instrument == instrument)
            : segment.Notes.Any(n => n.Step == step && n.Value == value && !n.IsCut);
        if (duplicate) return null;

        var note = _state.AddNote(segment, step, instrument, value, isCut);
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

    /// <summary>Continuous, unsnapped step - the marquee's counterpart to <see cref="StepAt" />.</summary>
    private double UnsnappedStepAt(float absX)
    {
        return _geometry.UnsnappedStepAt(absX - Computed.AbsoluteX);
    }

    /// <summary>Continuous, unsnapped value - the marquee's counterpart to <see cref="ValueAt(float)" />.</summary>
    private double UnsnappedValueAt(float absY)
    {
        return _geometry.UnsnappedValueAt(absY - Computed.AbsoluteY);
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

    // Marquee (Select tool): model-space anchor/cursor (continuous step, value), so
    // mid-drag scrolling can't corrupt it and off-screen notes inside the box still
    // count. Mode is sampled from the modifier bools at press, applied at release.
    private enum MarqueeMode
    {
        Replace,
        Append,
        Remove
    }

    // Group note drag: every selected note's starting (global step, value), captured
    // once at press. Each drag frame re-derives the anchor's (pressed note's) delta
    // from its own start and applies that same delta to every entry - this is what
    // makes dragging one note of a multi-selection move the whole group together.
    private readonly record struct GroupDragEntry(Note Note, int StartGlobalStep, double StartValue);
}