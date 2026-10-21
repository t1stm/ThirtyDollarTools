using EditorScene.State;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
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
public sealed partial class TrackEditorView : Panel
{
    public const int MaxValue = TrackEditorGeometry.MaxValue;
    public const int Rows = TrackEditorGeometry.Rows;
    public const float GutterWidth = TrackEditorGeometry.GutterWidth;
    private const float StripHeight = TrackEditorGeometry.StripHeight;
    private const float RulerHeight = TrackEditorGeometry.RulerHeight;
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
    // ≤7 slots per generated automation event (its band, seam, the line into it, and its
    // marker's shadow, fill, ring and outline), and the marks are viewport-culled, so this
    // bounds what one screen can show rather than what a note can generate. Past it the
    // paths simply stop drawing.
    internal const int AutomationMarkReserve = 4096;

    // The marker grab boxes: viewport-bounded like the marks they sit on, and far fewer,
    // since only the markers actually on screen are catchable.
    private const int KeyframeBlockPool = 256;

    // Everything flat-colored on this grid is batched - the line work, the bands, and the
    // note/strip pools' fills - and ascending slot order IS paint order within a batch.
    // There are two: the grid batch paints under every block, the block batch over it.
    //
    // Grid batch: the cut row's band first (every later slot paints over it), the line
    // work, then the zero-value band over that.
    private const int CutRowBgSlot = 0;
    private const int RowLineSlot = CutRowBgSlot + 1;
    private const int StepLineSlot = RowLineSlot + Rows + 1;
    private const int BoundaryLineSlot = StepLineSlot + StepLinePool;
    private const int ZeroRowSlot = BoundaryLineSlot + BoundaryLinePool;
    private const int GridBatchReserve = ZeroRowSlot + 1;

    // Block batch: the notes, the automation marks over the bodies they belong to, then
    // the strip/ruler bands over anything a note bleeds above the grid into, then the
    // segment strips on top of those. The pieces that must paint over ALL of it - the
    // gutter, the cut rule, the playheads, the marquee - stay ordinary children, which
    // render a layer above both batches.
    // A note takes three slots: its shadow, its body, then its selection ring - the ring
    // last, since with no margin to spare it is drawn inside the body's edge.
    private const int NoteBlockSlot = 0;
    private const int SlotsPerNote = 3;
    private const int AutomationMarkSlot = NoteBlockSlot + NoteBlockPool * SlotsPerNote;
    private const int ResizeHandleSlot = AutomationMarkSlot + AutomationMarkReserve;
    private const int StripBgSlot = ResizeHandleSlot + 1;
    private const int RulerBgSlot = StripBgSlot + 1;
    private const int StripBlockSlot = RulerBgSlot + 1;
    private const int BlockBatchTotal = StripBlockSlot + StripBlockPool;

    // The note look (docs: the shading plan's "Final" boards): how strong the light and the
    // volume fill are, how far a selected note lifts its colour, the tile's corner and its
    // drop shadow - 1 px straight down, and none once a step is too narrow to show it.
    private const float Shading = 1f;
    private const float SelectedLift = 0.16f;
    internal const float NoteRadius = 3f;
    private const float ShadowAlpha = 0.6f;
    internal const float MinShadowPixelsPerStep = 4f;

    /// <summary>
    ///     A rounded box's corner, kept in proportion as the rows zoom: <paramref name="radius" />
    ///     at <paramref name="atHeight" /> px tall (the boards' 20 px rows), scaled by the box's
    ///     own height, and never more than half its short side.
    /// </summary>
    internal static float CornerRadius(float width, float height, float radius = NoteRadius, float atHeight = 17f)
    {
        return Math.Min(radius * height / atHeight, Math.Min(width, height) / 2);
    }

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
    private readonly Marquee _marquee;
    private readonly List<KeyframeBlock> _keyframeBlocks = [];
    private readonly List<NoteBlock> _noteBlocks = [];
    private readonly List<float> _playheadXs = [];
    private readonly List<Panel> _playheads = [];
    private readonly EditorState _state;
    private readonly List<StripBlock> _stripBlocks = [];

    private NoteBlock? _dragging;

    /// <summary>Test seam: the hovered border band's view-local rect, zero-sized when none.</summary>
    private Vector4 _handleBand;

    private Vector4i? _inheritedClip;

    public TrackEditorView(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        Focusable = true;
        // Set here rather than in the markup: the draw code below cannot paint anything
        // without this rule's colors, so it must not depend on a usage site listing it.
        Classes = ["note-canvas"];

        // The whole canvas - lines, bands, note and strip fills - renders as two instanced
        // draw calls (see LineBatch), queued in DrawSelf below Children in the same depth
        // layer; the slot ranges above are what carry the paint order.
        _lineBatch.Count = GridBatchReserve;
        _blockBatch.Count = BlockBatchTotal;

        // Automation marks paint over the note blocks and never take input, so they are
        // batch slots only - no elements at all, just the reserve above them.
        _automationPath = new AutomationPath(_blockBatch, AutomationMarkSlot, AutomationMarkReserve, VolumeFill);

        // The note and strip blocks stay elements, but only to be hit: each owns the
        // batch slot its fill is written into (pool position = slot), so nothing has to
        // track which note ended up where.
        for (var i = 0; i < NoteBlockPool; i++)
        {
            var block = new NoteBlock(context, this) { BatchSlot = NoteBlockSlot + i * SlotsPerNote + 1 };
            _noteBlocks.Add(block);
            AddChild(block);
        }

        // Added after the note pool on purpose: a marker sitting on a note body has to win
        // the press over the body it is drawn on, and equal-depth hits resolve to the last
        // child tested (see Panel.HitTest).
        for (var i = 0; i < KeyframeBlockPool; i++)
        {
            var marker = new KeyframeBlock(context, this);
            _keyframeBlocks.Add(marker);
            AddChild(marker);
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
        // one text buffer and one draw call (see LabelBatch), rather than one Label, and
        // one draw call, per slot at every zoom.
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
        // uses for its playhead).
        _marquee = new Marquee(context);
        AddChild(_marquee.Rect);
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

    /// <summary>The cap marking where an automation's "cut at end" stops the note.</summary>
    [NamedSetting("automation-end-color")]
    public Vector4 AutomationEndColor { get; set; }

    /// <summary>The canvas's own background: what a quiet note's fill sinks toward.</summary>
    [NamedSetting("grid-color")]
    public Vector4 GridColor { get; set; }

    /// <summary>How far toward <see cref="GridColor" /> a silent note's fill sinks; a full one doesn't.</summary>
    [NamedSetting("volume-depth")]
    public float VolumeDepth { get; set; } = 0.62f;

    /// <summary>The ring a selected note (and its keyframe markers) is drawn with.</summary>
    [NamedSetting("selection-ring-color")]
    public Vector4 SelectionRingColor { get; set; }

    /// <summary>
    ///     Stable per-sound note colors: an instrument's name picks its index (hashed here
    ///     rather than with string.GetHashCode, which is randomized per process).
    /// </summary>
    [NamedSetting("sound-palette")]
    public Vector4[] SoundPalette { get; set; } = [];

    internal IReadOnlyList<AutomationPath.Mark> AutomationMarks => _automationPath.Marks;

    /// <summary>Test seam: the keyframe markers the last layout made draggable.</summary>
    /// <summary>The model the blocks route their edits through.</summary>
    internal EditorState State => _state;

    /// <summary>The note block a drag has hold of; null while none is running.</summary>
    internal NoteBlock? Dragging => _dragging;

    internal IReadOnlyList<AutomationPath.MarkerHandle> KeyframeHandles => _automationPath.Handles;
    internal IReadOnlyList<NoteBlock> NoteBlocks => _noteBlocks;
    internal Vector4 ResizeHandleBand => _handleBand;
    internal IReadOnlyList<KeyframeBlock> KeyframeBlocks => _keyframeBlocks;
    internal IReadOnlyList<LabelBatch.Slot> BeatLabels => _labelBatch.Range(BeatLabelSlot, BeatLabelPool);
    internal IReadOnlyList<LabelBatch.Slot> GutterLabels => _labelBatch.Range(GutterLabelSlot, Rows);

    /// <summary>One layer past the children, where this view's captions are queued.</summary>
    private int LabelLayer => Index + 2;

    /// <summary>The fill a pooled block currently paints with - it lives in the batch, not on the element.</summary>
    internal Vector4 FillOf(NoteBlock block)
    {
        return _blockBatch.ColorOf(block.BatchSlot);
    }

    /// <summary>Test seam: the rect a pooled block's tile was drawn at, in absolute px.</summary>
    internal Vector4 BlockRectOf(NoteBlock block)
    {
        return _blockBatch.RectOf(block.BatchSlot);
    }

    /// <summary>Test seam: a block's selection ring - its rect in absolute px, and its color (transparent when hidden).</summary>
    internal (Vector4 Rect, Vector4 Color, Vector4 Shape) RingOf(NoteBlock block)
    {
        var slot = block.BatchSlot + 1;
        return (_blockBatch.RectOf(slot), _blockBatch.ColorOf(slot), _blockBatch.ShapeOf(slot));
    }

    /// <summary>Test seam: a block's drop shadow, as <see cref="RingOf" />.</summary>
    internal (Vector4 Rect, Vector4 Color, Vector4 Shape) ShadowOf(NoteBlock block)
    {
        var slot = block.BatchSlot - 1;
        return (_blockBatch.RectOf(slot), _blockBatch.ColorOf(slot), _blockBatch.ShapeOf(slot));
    }

    /// <summary>Test seam: the shape a block's tile is drawn with.</summary>
    internal Vector4 BlockShapeOf(NoteBlock block)
    {
        return _blockBatch.ShapeOf(block.BatchSlot);
    }

    /// <summary>Test seam: the scroll, zoom and pixel mapping the last layout used.</summary>
    internal TrackEditorGeometry Geometry => _geometry;

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
    private bool FollowPlayhead { get; } = true;

    /// <summary>The same colour lifted toward white, alpha untouched - what reads on top of it.</summary>
    private static Vector4 Lighten(Vector4 color, float amount)
    {
        var lightened = color + (Vector4.One - color) * amount;
        lightened.W = color.W;
        return lightened;
    }

    /// <summary>The same colour mixed toward another by <paramref name="amount" />, alpha untouched.</summary>
    private static Vector4 Mix(Vector4 color, Vector4 toward, float amount)
    {
        var mixed = Vector4.Lerp(color, toward, amount);
        mixed.W = color.W;
        return mixed;
    }

    /// <summary>A note's own colour: its instrument's, lifted while it is selected.</summary>
    private Vector4 NoteColor(Note note, bool selected)
    {
        var color = InstrumentColor(note.Instrument);
        return selected ? Lighten(color, SelectedLift) : color;
    }

    /// <summary>
    ///     A note body's fill at a volume: its colour, sunk toward the grid as the volume drops.
    ///     A null volume plays at 100, and anything above 100 stays at full.
    /// </summary>
    internal Vector4 VolumeFill(Vector4 color, double? volume)
    {
        var quiet = 1 - Math.Clamp((volume ?? 100) / 100, 0, 1);
        return Mix(color, GridColor, (float)(VolumeDepth * Shading * quiet));
    }

    private static void Hide(UIElement element)
    {
        element.Width = 0;
        element.Height = 0;
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
        // One layer past the children: the gutter values and beat numbers paint over the
        // gutter/ruler backgrounds, which are children (Index + 1).
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
        DrawResizeHandle(uiContext);
        if (_placing != null && uiContext.CapturedElement != this) _placing = null;

        if (_marquee.Active && uiContext.CapturedElement != this)
        {
            CommitMarquee();
            _marquee.End();
            InvalidateLayout();
        }

        if (_keyframeDrag != null && uiContext.CapturedElement != _keyframeDrag)
        {
            _keyframeDrag = null;
            InvalidateLayout();
        }

        if (_dragging == null || uiContext.CapturedElement == _dragging) return;

        _dragging = null; // drag ended: let the pool reassign freely again
        _groupDrag = null;
        _resizeEdge = 0;
        InvalidateLayout();
    }

    /// <summary>Maps an absolute x to (segment, local step); null outside the track's grid.</summary>
    private (TrackSegment? segment, int step) StepAt(float absX, bool clamp)
    {
        return _geometry.StepAt(_state.OpenedTrack, absX - Computed.AbsoluteX, clamp);
    }

    private double ValueAt(float absY)
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
        return PaletteColor(instrument.Name, SoundPalette);
    }

    /// <summary>
    ///     A name's stable entry in a palette - hashed here rather than with
    ///     string.GetHashCode, which is randomized per process. The arrangement's faithful
    ///     miniatures color their sounds the same way.
    /// </summary>
    internal static Vector4 PaletteColor(string name, Vector4[] palette)
    {
        if (palette.Length == 0) return default;
        var hash = 0;
        foreach (var c in name) hash = hash * 31 + c;
        return palette[(hash & 0x7fffffff) % palette.Length];
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
}