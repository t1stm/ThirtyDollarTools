using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;
using VisualizerScene.Settings;

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
    private const int AutomationMarkReserve = 768; // ≤3 marks per generated automation event

    // Everything flat-colored on this grid is batched - the line work, the bands, and the
    // note/strip pools' fills - and ascending slot order IS paint order within a batch.
    // That order is why there are two: the automation paths have no bound (a note can
    // generate any number of events), and only a batch's LAST range can grow, but they
    // must still paint under the notes. So the grid batch holds everything up to and
    // including automation, and the block batch picks up from the notes.
    //
    // Grid batch: the cut row's band first (every later slot paints over it), the line
    // work, the zero-value band over that, then the automation paths.
    private const int CutRowBgSlot = 0;
    private const int RowLineSlot = CutRowBgSlot + 1;
    private const int StepLineSlot = RowLineSlot + Rows + 1;
    private const int BoundaryLineSlot = StepLineSlot + StepLinePool;
    private const int ZeroRowSlot = BoundaryLineSlot + BoundaryLinePool;
    private const int AutomationMarkSlot = ZeroRowSlot + 1;
    private const int GridBatchReserve = AutomationMarkSlot + AutomationMarkReserve;

    // Block batch: the notes, then the strip/ruler bands over anything a note bleeds
    // above the grid into, then the segment strips on top of those. The pieces that must
    // paint over ALL of it - the gutter, the cut rule, the playheads, the marquee - stay
    // ordinary children, which render a layer above both batches.
    private const int NoteBlockSlot = 0;
    private const int StripBgSlot = NoteBlockSlot + NoteBlockPool;
    private const int RulerBgSlot = StripBgSlot + 1;
    private const int StripBlockSlot = RulerBgSlot + 1;
    private const int BlockBatchTotal = StripBlockSlot + StripBlockPool;

    // ponytail: worst case (StepsPerBeat=1 at 4px/step min zoom) would need ~750 like
    // StepLinePool, but MinBeatLabelSpacingPx bounds real usage to ~110 labels for a
    // 3000px viewport, so 128 is generous without paying for a 768-sized label pool.
    private const int BeatLabelPool = 128;
    private const float MinBeatLabelSpacingPx = 28f;

    // Slot ranges within _labelBatch, in the same spirit as the line batch's above:
    // the beat ruler's numbers, the value gutter's, then the pinned cut row's caption.
    private const int BeatLabelSlot = 0;
    private const int GutterLabelSlot = BeatLabelSlot + BeatLabelPool;
    private const int CutLabelSlot = GutterLabelSlot + Rows;
    private const int LabelBatchTotal = CutLabelSlot + 1;

    private readonly AutomationPath _automationPath;
    private readonly LineBatch _blockBatch = new();
    private readonly Panel _cutRule;
    private readonly string[] _gutterText = new string[Rows];
    private readonly LabelBatch _labelBatch;
    private readonly TrackEditorGeometry _geometry = new();
    private readonly Panel _gutterBackground;
    private readonly LineBatch _lineBatch = new();
    private readonly Panel _marqueeRect;
    private readonly List<NoteBlock> _noteBlocks = [];
    private readonly List<float> _playheadXs = [];
    private readonly List<Panel> _playheads = [];
    internal readonly EditorState _state;
    private readonly List<StripBlock> _stripBlocks = [];

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
        // Set here rather than in the markup: the draw code below cannot paint anything
        // without this rule's colors, so it must not depend on a usage site listing it.
        Classes = ["note-canvas"];

        // The whole canvas - lines, bands, note and strip fills - renders as two instanced
        // draw calls (see LineBatch), queued in DrawSelf below Children in the same depth
        // layer; the slot ranges above carry the paint order that adding these as siblings
        // in the right sequence used to.
        _lineBatch.Count = GridBatchReserve;
        _blockBatch.Count = BlockBatchTotal;

        // Automation paths paint under the note blocks and never take input, so they are
        // batch slots only - no elements at all, and no cap: they are the grid batch's
        // last range, which grows.
        _automationPath = new AutomationPath(_lineBatch, AutomationMarkSlot);

        // The note and strip blocks stay elements, but only to be hit: each owns the
        // batch slot its fill is written into (pool position = slot), so nothing has to
        // track which note ended up where.
        for (var i = 0; i < NoteBlockPool; i++)
        {
            var block = new NoteBlock(context, this) { BatchSlot = NoteBlockSlot + i };
            _noteBlocks.Add(block);
            AddChild(block);
        }

        for (var i = 0; i < StripBlockPool; i++)
        {
            var block = new StripBlock(context, this) { BatchSlot = StripBlockSlot + i };
            _stripBlocks.Add(block);
            AddChild(block);
        }

        var gutter = new Panel(context)
        {
            Classes = ["grid-gutter"],
            OnClick = _ => { } // swallow: never place notes through the gutter
        };
        _gutterBackground = gutter;
        AddChild(gutter);

        // Every caption on this grid - ruler beats, gutter values, the cut row's - shares
        // one text buffer and one draw call (see LabelBatch), rather than one Label (and
        // one draw call) each, live at every zoom whether or not it had anything to say.
        _labelBatch = new LabelBatch(context, LabelBatchTotal);
        for (var i = 0; i < Rows; i++)
        {
            var value = MaxValue - i;
            _gutterText[i] = value switch
            {
                > 0 => $"+{value}",
                < 0 => $"{value}",
                _ => $" {value}"
            };
        }

        // Added after the gutter so the rule spans the full width,
        // crossing the gutter column too, instead of being cut off by it.
        _cutRule = NewGhost(context, "grid-cut-rule");
        AddChild(_cutRule);

        // Added last so it renders above every note block (same trick ArrangementView
        // uses for its playhead); never takes input (GhostPanel), fill only, no border.
        _marqueeRect = new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Classes = ["grid-marquee"]
        };
        AddChild(_marqueeRect);
    }

    // The grid's own colors, from `class note-canvas` (Scenes/Views/GridViews.snx.ss).
    // These paint the LineBatch and the pooled note/strip/label fills rather than styled
    // elements, so no selector can reach them - they arrive as settings on the view
    // itself instead, and the draw code below reads them off `this`.

    /// <summary>Beat numbers on the ruler, and the beat the playhead is currently in.</summary>
    [NamedSetting("label-color")]
    public Vector4 LabelColor { get; set; }

    /// <inheritdoc cref="LabelColor" />
    [NamedSetting("playhead-color")]
    public Vector4 PlayheadColor { get; set; }

    /// <summary>The line work, faintest first.</summary>
    [NamedSetting("step-line-color")]
    public Vector4 StepLineColor { get; set; }

    /// <inheritdoc cref="StepLineColor" />
    [NamedSetting("row-line-color")]
    public Vector4 RowLineColor { get; set; }

    /// <inheritdoc cref="StepLineColor" />
    [NamedSetting("octave-line-color")]
    public Vector4 OctaveLineColor { get; set; }

    /// <inheritdoc cref="StepLineColor" />
    [NamedSetting("beat-line-color")]
    public Vector4 BeatLineColor { get; set; }

    /// <summary>Segment boundaries, and the rule under the pinned !cut row.</summary>
    [NamedSetting("boundary-color")]
    public Vector4 BoundaryColor { get; set; }

    /// <summary>The !cut row's own band.</summary>
    [NamedSetting("cut-row-color")]
    public Vector4 CutRowColor { get; set; }

    /// <summary>The band behind value 0.</summary>
    [NamedSetting("zero-row-color")]
    public Vector4 ZeroRowColor { get; set; }

    /// <summary>The bands the segment strip and the beat ruler sit on.</summary>
    [NamedSetting("strip-color")]
    public Vector4 StripColor { get; set; }

    /// <summary>Segment strips alternate between these two, so a boundary needs no line.</summary>
    [NamedSetting("strip-segment-a")]
    public Vector4 StripSegmentA { get; set; }

    /// <inheritdoc cref="StripSegmentA" />
    [NamedSetting("strip-segment-b")]
    public Vector4 StripSegmentB { get; set; }

    /// <summary>The selected segment's strip.</summary>
    [NamedSetting("strip-selected-color")]
    public Vector4 StripSelected { get; set; }

    /// <summary>A note block's fill while it is part of the selection.</summary>
    [NamedSetting("selected-note-color")]
    public Vector4 SelectedNoteColor { get; set; }

    /// <summary>
    ///     Stable per-sound note colors: an instrument's name picks its index (hashed here
    ///     rather than with string.GetHashCode, which is randomized per process).
    /// </summary>
    [NamedSetting("sound-palette")]
    public Vector4[] SoundPalette { get; set; } = [];

    internal IReadOnlyList<Vector4> AutomationMarks => _automationPath.Marks;
    internal IReadOnlyList<NoteBlock> NoteBlocks => _noteBlocks;
    internal IReadOnlyList<LabelBatch.Slot> BeatLabels => _labelBatch.Range(BeatLabelSlot, BeatLabelPool);
    internal IReadOnlyList<LabelBatch.Slot> GutterLabels => _labelBatch.Range(GutterLabelSlot, Rows);

    /// <summary>One layer past the children, where this view's captions are queued.</summary>
    private int LabelLayer => Index + 2;

    /// <summary>The fill a pooled block currently paints with - it lives in the batch, not on the element.</summary>
    internal Vector4 FillOf(NoteBlock block)
    {
        return _blockBatch.ColorOf(block.BatchSlot);
    }

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
    public Action<Note, double?>? OnPreviewNote { get; set; }

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

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        _lineBatch.Set(CutRowBgSlot, absX, absY + cutRowTop, width, CutRowHeight, CutRowColor);

        var zeroRowY = _geometry.ValueTop(0);
        _lineBatch.Set(ZeroRowSlot, absX + GutterWidth, absY + zeroRowY, gridWidth,
            Math.Max(0, Math.Min(zeroRowY + rowHeight, gridBottom) - zeroRowY), ZeroRowColor);

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
                    var x = GutterWidth + segStart - scrollX;
                    block.X = x;
                    block.Y = 2;
                    block.Width = segWidth - 1;
                    block.Height = StripHeight - 4;
                    _blockBatch.Set(block.BatchSlot, absX + x, absY + 2, segWidth - 1, StripHeight - 4,
                        segment == _state.SelectedSegment
                            ? StripSelected
                            : i % 2 == 0
                                ? StripSegmentA
                                : StripSegmentB);
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
                for (var s = firstBeatLocal; s <= lastLocal && beatLabel < BeatLabelPool; s += segment.StepsPerBeat)
                {
                    var bx = GutterWidth + segStart + s * pps - scrollX;
                    if (bx - lastLabelX < MinBeatLabelSpacingPx) continue;
                    lastLabelX = bx;

                    var beatWidthPx = segment.StepsPerBeat * pps;
                    var isCurrent = _playheadXs.Exists(px => px >= bx && px < bx + beatWidthPx);
                    _labelBatch.Set(BeatLabelSlot + beatLabel++,
                        $"{segBeatStart + s / segment.StepsPerBeat + 1}",
                        absX + bx + 2, absY + StripHeight + (RulerHeight - LabelBatch.FontSize) / 2,
                        isCurrent ? PlayheadColor : LabelColor);
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
                        _automationPath.Draw(_geometry, (absX, absY), track, segment, note, segStart,
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

        // A released block gives up both its hit box and its fill slot.
        for (var i = noteBlock; i < _noteBlocks.Count; i++)
        {
            if (_noteBlocks[i] == _dragging) continue;
            Hide(_noteBlocks[i]);
            _blockBatch.Hide(NoteBlockSlot + i);
        }

        _automationPath.HideUnused(autoMark);
        for (var i = stepLine; i < StepLinePool; i++) _lineBatch.Hide(StepLineSlot + i);
        for (var i = stripBlock; i < _stripBlocks.Count; i++)
        {
            Hide(_stripBlocks[i]);
            _blockBatch.Hide(StripBlockSlot + i);
        }

        for (var i = boundary; i < BoundaryLinePool; i++) _lineBatch.Hide(BoundaryLineSlot + i);
        for (var i = beatLabel; i < BeatLabelPool; i++) _labelBatch.Hide(BeatLabelSlot + i);

        // The bands the strip and ruler sit on: batch slots after every note's, so a note
        // scrolled above the grid is masked by them exactly as it was when they were
        // children added before the note pool.
        _blockBatch.Set(StripBgSlot, absX, absY, width, StripHeight, StripColor);
        _blockBatch.Set(RulerBgSlot, absX, absY + StripHeight, width, RulerHeight, StripColor);

        _gutterBackground.X = 0;
        _gutterBackground.Y = 0;
        _gutterBackground.Width = GutterWidth;
        _gutterBackground.Height = height;

        // The labels are 11px tall: below a 10px row they collide, so only every Nth
        // value keeps its label, spaced ~12px apart (every 3rd at the 4px floor).
        // Striding on the value, not the index, keeps 0 labelled at every zoom.
        var labelStride = rowHeight >= 10f ? 1 : (int)MathF.Ceiling(12f / rowHeight);

        for (var i = 0; i < Rows; i++)
        {
            var value = MaxValue - i;
            var y = _geometry.ValueTop(value) + (rowHeight - LabelBatch.FontSize) / 2;
            // A scrolled-out (or skipped) value releases its slot rather than relying on
            // paint order: the gutter labels render above the strip background.
            if (value % labelStride != 0 || y < GridTop || y + LabelBatch.FontSize > gridBottom)
                _labelBatch.Hide(GutterLabelSlot + i);
            else
                _labelBatch.Set(GutterLabelSlot + i, _gutterText[i], absX + 8, absY + y, LabelColor);
        }

        // Fixed position - always visible, never released like the scrollable gutter
        // labels above.
        _labelBatch.Set(CutLabelSlot, "!cut", absX + 8,
            absY + cutRowTop + (CutRowHeight - LabelBatch.FontSize) / 2, LabelColor);

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
                var created = NewGhost(Context, "grid-playhead");
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
        float x, y, blockWidth, blockHeight;
        if (note.IsCut)
        {
            // Fixed row - never scrolls, unlike every other row. Simultaneous cuts on
            // different instruments (cutSlotCount > 1) split the step evenly instead of
            // stacking exactly on top of each other.
            var slotWidth = PixelsPerStep / cutSlotCount;
            x = stepX + cutSlot * slotWidth;
            y = _geometry.CutRowTop + 0.5f;
            blockWidth = Math.Max(3, slotWidth - 1);
            blockHeight = Math.Max(3, CutRowHeight - 1);
        }
        else
        {
            x = stepX;
            y = _geometry.ValueTop(note.Value) + 0.5f;
            blockWidth = Math.Max(3, PixelsPerStep - 1);
            // No overdraw covers a grid note bleeding past the grid's bottom edge
            // (unlike the top, still covered by the strip/ruler bands), so clamp it.
            var naturalHeight = Math.Max(3, _geometry.RowHeight - 1);
            blockHeight = Math.Max(0, Math.Min(y + naturalHeight, _geometry.GridBottom) - y);
        }

        // The element carries the hit box; the batch slot carries the fill.
        block.X = x;
        block.Y = y;
        block.Width = blockWidth;
        block.Height = blockHeight;
        _blockBatch.Set(block.BatchSlot, Computed.AbsoluteX + x, Computed.AbsoluteY + y, blockWidth, blockHeight,
            _state.SelectedNotes.Contains(note) ? SelectedNoteColor : InstrumentColor(note.Instrument));
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
        // Escape clears the selection first; only once nothing is selected does it fall
        // through to closing the track (see Editor.KeyDown for the same chain when this
        // view isn't the focused element). A chain's head isn't a shortcut, so it stays
        // out of the bind table and ahead of it.
        if (e.Key == Keys.Escape)
        {
            if (_state.SelectedNotes.Count > 0) _state.ClearSelection();
            else _state.CloseTrack();
            return true;
        }

        switch (Keybinds.Match(e, BindScene.Editor))
        {
            case Bind.EditorDelete or Bind.EditorDeleteAlt when _state.SelectedNotes.Count > 0:
                _state.DeleteSelection();
                return true;
            case Bind.EditorCopy:
                _state.CopySelection();
                return true;
            case Bind.EditorPaste:
                _state.Paste();
                return true;
            case Bind.EditorCut:
                _state.CutSelection();
                return true;
            case Bind.EditorSelectAll:
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
        _blockBatch.ClipRect = own;
        _labelBatch.ClipRect = own;
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        // Same layer, queued in paint order: the grid (up to the automation paths) first,
        // then the notes and the bands over them.
        ctx.QueueRender(_lineBatch, Index);
        ctx.QueueRender(_blockBatch, Index);
        // One layer past the children: the gutter values and beat numbers are painted
        // over the gutter/ruler backgrounds, which are children (Index + 1) - the paint
        // order they had as children added after them.
        ctx.QueueRender(_labelBatch, LabelLayer);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        Context.DequeueRender(_lineBatch, Index);
        Context.DequeueRender(_blockBatch, Index);
        Context.DequeueRender(_labelBatch, LabelLayer);
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

        _groupDrag =
        [
            .. _state.SelectedNotes.Select(note =>
            {
                var segment = track.Segments.FirstOrDefault(s => s.Notes.Contains(note));
                var globalStep = segment != null ? track.GlobalStepOf(segment, note.Step) : note.Step;
                return new GroupDragEntry(note, globalStep, note.Value);
            })
        ];
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
        if (moved) OnPreviewNote?.Invoke(_dragging!.Note!, value);
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
        OnPreviewNote?.Invoke(note, null);
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

    private Vector4 InstrumentColor(Instrument instrument)
    {
        if (SoundPalette.Length == 0) return default;
        var hash = 0;
        foreach (var c in instrument.Name) hash = hash * 31 + c;
        return SoundPalette[(hash & 0x7fffffff) % SoundPalette.Length];
    }

    /// <summary>
    ///     A parked, input-transparent plane the layout resizes into some piece of grid
    ///     furniture. Its fill comes from <paramref name="className" /> once this view is
    ///     styled, which AddChild takes care of for a child added before that happens.
    /// </summary>
    private static Panel NewGhost(UIContext context, string className)
    {
        return new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Classes = [className]
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