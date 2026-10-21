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
///     The FL-style arrangement grid: channels are horizontal lanes, time runs right in
///     quarter notes at the root BPM. Every <see cref="TrackPlacement" /> is a draggable
///     clip; clicking an empty cell places the selected pattern there; Delete removes
///     the selected clip; the wheel pans horizontally. Double-clicking a clip fires
///     <see cref="OnOpenTrack" /> (the future per-track note editor).
/// </summary>
public sealed partial class ArrangementView : Panel
{
    public const float LaneHeight = 44f;
    public const float RulerHeight = 18f;
    private const int MinChannels = 8;

    // ponytail: fixed pools - 128 bar lines cover a 3k px window at minimum zoom (6 ppq).
    // LaneLinePool is also the LaneHeader's toggle pool and the bound ChannelCount clamps
    // to, so lane count, divider lines and M/S toggles all stay in step; a channel past it
    // would get a clip block but no line or toggles. 128 is generous headroom over any
    // realistic track count.
    private const int BarLinePool = 128;
    public const int LaneLinePool = 128;

    // Slot ranges within _lineBatch: the lane dividers, the bar lines, then the clips
    // over both of them. Ascending slot order is paint order. The clip range is last
    // because it has no bound - a project can hold any number of placements - and only a
    // batch's last range grows; the ranges before it are fixed pools. A clip takes four
    // slots - its shadow, name strip, body and selection ring - and what the clips hold
    // (wave peaks, note and event miniatures) follows them all, in a tail of its own.
    private const int LaneLineSlot = 0;
    private const int BarLineSlot = LaneLineSlot + LaneLinePool;
    private const int ClipBlockSlot = BarLineSlot + BarLinePool;
    private const int SlotsPerClip = 4;
    private const int LineBatchReserve = ClipBlockSlot + 64 * SlotsPerClip;

    // A clip is a tile centred in its lane by the note editor's rule: 1 px clear of the lane's
    // lines, its hit box the whole lane span. Its top 18 px are a name strip in the track's
    // color, over a body sunk halfway toward the grid that holds a miniature of what it plays.
    // The boards' numbers: the corner, the strip, the body's depth, the light's strength and
    // the shadow, 2 px straight down.
    private const float ClipRadius = 5f;
    private const float ClipHeight = LaneHeight - 3;
    private const float StripHeight = 18f;
    private const float BodyDepth = 0.5f;
    private const float Shading = 1f;
    private const float ClipShadowAlpha = 0.55f;
    private const float ClipShadowDrop = 2f;

    // A wave clip's peak envelope: 2 px bars every 3 px, in the body's 16 px of room
    // (clip rows 21 to 37), lightened off the clip's own color. The budget is per clip, not per frame:
    // a shared one is spent by whichever clips are laid out first, which leaves every
    // lane past it blank in a project of stems. A clip's visible bars are already bounded
    // by the view's width / WaveBarPitch, so the budget only ever binds on an absurdly
    // wide window - it is a backstop against the pool growing without bound, not a cap
    // the normal case reaches.
    private const float WaveBarPitch = 3f;
    private const float WaveBarWidth = 2f;
    private const float WaveBarTop = 21f;
    private const float WaveBarRoom = 16f;
    private const float WaveBarLift = 0.15f;
    private const int WaveBarBudget = 2048;

    // A clip's name, in its strip: the inset and font size the batched label draws with,
    // since no stylesheet rule can reach a batch slot. It sits 1 px below the clip's top.
    private const float ClipPadding = 6f;
    private const float ClipFontSize = 13f;
    private const float ClipLabelTop = 1f;

    // How far toward white a selected clip's color is lifted - the same lift a selected note
    // takes. The ring marks the selection; the lift is the second cue, small enough that the
    // track's own color still reads.
    private const float SelectedLift = 0.16f;

    private readonly LabelBatch _barLabels;

    private readonly List<ClipBlock> _blocks = [];
    private readonly LabelBatch _clipLabels;
    private readonly LineBatch _lineBatch = new();
    private readonly Marquee _marquee;

    private readonly ViewNavigation _nav = new(6f, 96f) { Zoom = 24f };
    private readonly Panel _playhead;
    private readonly Panel _rulerBackground;
    private readonly EditorState _state;
    private int _clipSlotsUsed;
    private int _tailSlotStart;
    private int _tailSlotsUsed;
    private int _waveBarsDrawn;

    /// <summary>
    ///     The wave clips of the current layout pass, with the rectangle they were just laid
    ///     out at - reused between frames, since a clip's element carries its geometry as
    ///     layout values rather than numbers this pass can read back.
    /// </summary>
    private readonly List<(WaveTrack Track, Vector4 Tile)> _waveClips = [];

    private Vector4i? _inheritedClip;
    private bool _refreshDeferred;

    public ArrangementView(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        Focusable = true;
        // Set here rather than in the markup - see TrackEditorView's.
        Classes = ["arrangement-canvas"];
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

        // Grid lines and clip fills render as one instanced draw call (see LineBatch)
        // queued in DrawSelf, below Children in the same depth layer - so the ruler, the
        // playhead and the marquee, which are children, still paint above the clips.
        _lineBatch.Count = LineBatchReserve;

        // The beat ruler - same idea as the note editor's: a strip along the top
        // labeling every bar, highlighting whichever bar the playhead is in.
        _rulerBackground = new GhostPanel(context) { Classes = ["grid-strip"] };
        AddChild(_rulerBackground);
        // One text buffer and one draw call for the whole ruler (see LabelBatch), rather
        // than a Label - and a draw call - per pool slot.
        _barLabels = new LabelBatch(context, BarLinePool);

        // The clips' track names, likewise: one call for the lot rather than one per clip.
        // Their own batch because they are clipped to the lanes, not to the whole view -
        // a name must never paint over the ruler.
        _clipLabels = new LabelBatch(context, 64, ClipFontSize, 24);

        _playhead = new GhostPanel(context)
        {
            Width = 0,
            Height = 0,
            Classes = ["grid-playhead"]
        };
        AddChild(_playhead);

        // A child, so it renders above the batched clips by layer.
        _marquee = new Marquee(context);
        AddChild(_marquee.Rect);

        Refresh();
    }

    // The grid's own colors, from `class arrangement-canvas` (Scenes/Views/GridViews.snx.ss).
    // These paint a LineBatch and pooled label fills rather than styled elements, so no
    // selector can reach them - they arrive as settings on the view itself instead.

    /// <summary>Lane dividers and bar lines.</summary>
    [NamedSetting("line-color")]
    public Vector4 LineColor { get; set; }

    /// <summary>The ruler's bar numbers.</summary>
    [NamedSetting("label-color")]
    public Vector4 LabelColor { get; set; }

    /// <summary>The brighter shade the bar under the playhead takes.</summary>
    [NamedSetting("playhead-color")]
    public Vector4 PlayheadColor { get; set; }

    /// <summary>The fill of a clip whose track has no color of its own.</summary>
    [NamedSetting("clip-color")]
    public Vector4 ClipColor { get; set; }

    /// <summary>The track name written across a clip.</summary>
    [NamedSetting("clip-label-color")]
    public Vector4 ClipLabelColor { get; set; }

    /// <summary>The canvas's own background: what a clip's body sinks toward.</summary>
    [NamedSetting("grid-color")]
    public Vector4 GridColor { get; set; }

    /// <summary>The ring a selected clip is drawn with - the note editor's selection ring.</summary>
    [NamedSetting("selection-ring-color")]
    public Vector4 SelectionRingColor { get; set; }

    /// <summary>
    ///     The note editor's per-sound colors: a faithful clip's miniature colors each beat by
    ///     its sound, hashed over these the same way the note editor colors an instrument.
    /// </summary>
    [NamedSetting("sound-palette")]
    public Vector4[] SoundPalette { get; set; } = [];

    /// <summary>
    ///     The colors a track's <see cref="ProjectTrack.ColorIndex" /> picks from - the same
    ///     palette the note editor tints instruments with, so both grids read as one scheme.
    /// </summary>
    [NamedSetting("clip-palette")]
    public Vector4[] ClipPalette { get; set; } = [];

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
    /// <summary>
    ///     A wave file's peak envelope, by path; null while it is still decoding. Wired to
    ///     EditorPlayback, which owns the decode - the view never touches a file.
    /// </summary>
    public Func<string, float[]?>? WavePeaks { get; set; }

    internal IReadOnlyList<ClipBlock> Blocks => _blocks;

    /// <summary>Test seam: how many peak bars the last layout wrote.</summary>
    internal int WaveBarsDrawn => _waveBarsDrawn;

    /// <summary>Test seam: the slot a peak bar was written to, by index within the wave range.</summary>
    internal int WaveBarSlot(int index)
    {
        return _tailSlotStart + index;
    }

    /// <summary>Test seam: a clip's drawn tile, view-local (x, y, width, height).</summary>
    internal Vector4 TileOf(ClipBlock block)
    {
        return block.Tile;
    }

    /// <summary>Test seam: a clip's selection ring - its rect in absolute px and its color, transparent when hidden.</summary>
    internal (Vector4 Rect, Vector4 Color) RingOf(ClipBlock block)
    {
        return (_lineBatch.RectOf(block.BatchSlot + 2), _lineBatch.ColorOf(block.BatchSlot + 2));
    }

    /// <summary>Test seam: what a clip's body is painted with.</summary>
    internal Vector4 BodyOf(ClipBlock block)
    {
        return _lineBatch.ColorOf(block.BatchSlot + 1);
    }

    /// <summary>Test seam: what a batch slot is currently painted with; transparent when released.</summary>
    internal Vector4 BatchColorAt(int slot)
    {
        return _lineBatch.ColorOf(slot);
    }
    /// <summary>
    ///     A track's clip color - its name strip's: its palette entry, or <see cref="ClipColor" />
    ///     when it has no color of its own. The index wraps, so retuning the palette to fewer
    ///     entries recolors those tracks rather than throwing on an out-of-range one.
    ///     <paramref name="selected" /> lifts that same color rather than replacing it, so the
    ///     track's own color still reads under the selection ring.
    /// </summary>
    public Vector4 ColorOf(ProjectTrack track, bool selected = false)
    {
        var color = track.ColorIndex is { } index && ClipPalette.Length > 0
            ? ClipPalette[((index % ClipPalette.Length) + ClipPalette.Length) % ClipPalette.Length]
            : ClipColor;

        // Lerped toward white rather than scaled, so the lift lands the same on every
        // palette entry however dark or light it is. Alpha is carried through untouched.
        return selected ? new Vector4(Vector3.Lerp(color.Xyz, Vector3.One, SelectedLift), color.W) : color;
    }

    /// <summary>The color a clip's name strip currently paints with - it lives in the batch, not on the element.</summary>
    internal Vector4 FillOf(ClipBlock block)
    {
        return _lineBatch.ColorOf(block.BatchSlot);
    }

    internal LabelBatch ClipLabels => _clipLabels;

    /// <summary>Past every child, where this view's captions are queued.</summary>
    private int LabelLayer => Index + 2;

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
    public double SnapQuarterNotes { get; } = 1;

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
            var block = new ClipBlock(Context, this, placement)
            {
                BatchSlot = ClipBlockSlot + _blocks.Count * SlotsPerClip + 1,
                LabelSlot = _blocks.Count
            };
            _blocks.Add(block);
            AddChild(block);
        }

        // No sibling re-ordering needed: a clip paints from a batch slot below every
        // child, so the ruler (which must mask a clip scrolled up into it), the playhead
        // and the marquee are above it by layer, not by add order.
        RefreshSelection();
        InvalidateLayout();
    }

    public void RefreshSelection()
    {
        _selectionVersion++;
        foreach (var block in _blocks)
            block.Selected = _state.SelectedPlacements.Contains(block.Placement);

        InvalidateLayout(); // the selected shade is written with the fill, in DoLayout
    }

    protected override void DoLayout()
    {
        var lanes = ChannelCount;
        var width = Computed.Width;
        var gridHeight = Math.Max(0, Computed.Height - RulerHeight);
        var lanesBottom = lanes * LaneHeight;

        // Recomputed every layout so the next wheel/pan clamps against the current bound.
        // ScrollY itself is deliberately NOT re-clamped here (matching ScrollX, which is
        // never bounded on the right either - content can scroll past a shorter one):
        // removing a track shrinks the lane count on the very next layout pass, and pulling
        // ScrollY down to fit would scroll the view under a still-held pointer, sliding the
        // next track under a level-triggered right-press or delete-drag.
        _nav.MaxScrollY = Math.Max(0, lanesBottom - gridHeight);
        var scrollY = _nav.ScrollY;
        var visibleLanesBottom = Math.Max(0, Math.Min(lanesBottom - scrollY, gridHeight));

        var absX = Computed.AbsoluteX;
        var absY = Computed.AbsoluteY;

        for (var i = 0; i < LaneLinePool; i++)
        {
            var y = RulerHeight + (i + 1) * LaneHeight - scrollY;
            var visible = i < lanes && y >= RulerHeight && y <= Computed.Height;
            _lineBatch.Set(LaneLineSlot + i, absX, absY + y, visible ? width : 0, 1, LineColor);
        }

        var playheadX = (float)(PlayheadQuarters * PixelsPerQuarter) - _nav.ScrollX;
        var playheadVisible = PlayheadQuarters >= 0 && playheadX >= 0 && playheadX < width;

        var barWidth = 4 * PixelsPerQuarter;
        var firstBar = (int)Math.Floor(_nav.ScrollX / barWidth);
        for (var i = 0; i < BarLinePool; i++)
        {
            var x = (firstBar + i) * barWidth - _nav.ScrollX;
            var visible = x >= 0 && x < width;
            _lineBatch.Set(BarLineSlot + i, absX + x, absY + RulerHeight, visible ? 1 : 0,
                visibleLanesBottom, LineColor);

            if (!visible)
            {
                _barLabels.Hide(i);
                continue;
            }

            var isCurrentBar = playheadVisible && playheadX >= x && playheadX < x + barWidth;
            _barLabels.Set(i, $"{firstBar + i + 1}", absX + x + 3,
                absY + (RulerHeight - LabelBatch.FontSize) / 2,
                isCurrentBar ? PlayheadColor : LabelColor);
        }

        _waveClips.Clear();
        foreach (var block in _blocks)
        {
            var placement = block.Placement;
            var quarters = placement.Track.DurationMinutes() * _state.Project.RootTiming.BPM;
            var start = (float)(placement.StartQuarterNotes * PixelsPerQuarter) - _nav.ScrollX;
            // No top clamp: a clip scrolled up past the ruler bleeds to a negative Y, same
            // as a note bleeds past TrackEditorView's grid top, and is masked by the ruler
            // (which renders a layer above this batch). Clamping here would instead pin
            // every clip crossing the ruler band to the same Y, stacking their labels.
            var laneTop = RulerHeight + placement.Channel * LaneHeight - scrollY;
            var clipWidth = Math.Max(8, (float)(quarters * PixelsPerQuarter));

            // The element carries only the hit box - the lane span, line to line - and the
            // batch slots carry the tile centred in it, and the name (see TrackEditorView's
            // note blocks for the same split).
            block.X = start + 1;
            block.Y = laneTop + 1;
            block.Width = clipWidth - 1;
            block.Height = LaneHeight - 1;
            block.Tile = new Vector4(start + 2, laneTop + 2, clipWidth - 3, ClipHeight);
            DrawClip(block, absX, absY);
            if (placement.Track is WaveTrack wave) _waveClips.Add((wave, block.Tile));
            // Confine the name to its own clip's strip, per slot (see LabelBatch.Set's clip):
            // the batch's ClipRect is one scissor for the whole draw call and can only bound
            // the pool as a group, so a clip zoomed narrower than its name would otherwise
            // spill it over the clips beside it.
            var tile = block.Tile;
            _clipLabels.Set(block.LabelSlot, placement.Track.Name, absX + tile.X + ClipPadding,
                absY + tile.Y + ClipLabelTop, ClipLabelColor,
                new Vector4(absX + tile.X + ClipPadding, absY + tile.Y,
                    absX + tile.X + tile.Z - ClipPadding, absY + tile.Y + StripHeight));
        }

        // Slots left over from a longer clip list - both batches' tails grow with the
        // project and are never shrunk, so releasing them is what clears removed clips.
        for (var i = _blocks.Count; i < _clipSlotsUsed; i++)
        {
            for (var slot = 0; slot < SlotsPerClip; slot++) _lineBatch.Hide(ClipBlockSlot + i * SlotsPerClip + slot);
            _clipLabels.Hide(i);
        }

        _clipSlotsUsed = _blocks.Count;
        DrawContents(absX, absY, width);

        _playhead.Width = playheadVisible ? 2 : 0;
        _playhead.Height = visibleLanesBottom;
        _playhead.X = playheadX;
        _playhead.Y = RulerHeight;

        // The marquee's axes are (quarters, channel).
        _marquee.Layout((quarters, channel) => ((float)(quarters * PixelsPerQuarter) - _nav.ScrollX,
            RulerHeight + (float)(channel * LaneHeight) - _nav.ScrollY));

        _rulerBackground.X = 0;
        _rulerBackground.Y = 0;
        _rulerBackground.Width = width;
        _rulerBackground.Height = RulerHeight;

        base.DoLayout();
        ApplyClip(_inheritedClip);
    }

    /// <summary>
    ///     One clip's four slots: a drop shadow 2 px straight down, the name strip in the
    ///     track's color, the body under it sunk halfway toward the grid, and - while it is
    ///     selected - a ring around the whole lane span, which is the margin the tile keeps.
    /// </summary>
    private void DrawClip(ClipBlock block, float absX, float absY)
    {
        var tile = block.Tile;
        var (x, y) = (absX + tile.X, absY + tile.Y);
        var color = ColorOf(block.Placement.Track, block.Selected);
        var body = new Vector4(Vector3.Lerp(color.Xyz, GridColor.Xyz, BodyDepth * Shading), color.W);

        _lineBatch.Set(block.BatchSlot - 1, x, y + ClipShadowDrop, tile.Z, tile.W,
            new Vector4(0, 0, 0, ClipShadowAlpha * Shading), ShadedRect.Of(ClipRadius));
        _lineBatch.Set(block.BatchSlot, x, y, tile.Z, StripHeight, color,
            ShadedRect.Of(ClipRadius, ShadedRect.Top, strength: Shading, light: ShadedRect.Light.ClipStrip));
        _lineBatch.Set(block.BatchSlot + 1, x, y + StripHeight, tile.Z, tile.W - StripHeight, body,
            ShadedRect.Of(ClipRadius, ShadedRect.Bottom, strength: Shading, light: ShadedRect.Light.ClipBody));

        if (block.Selected)
            _lineBatch.Set(block.BatchSlot + 2, absX + block.X.Value, absY + block.Y.Value, block.Width.Value,
                block.Height.Value, SelectionRingColor, ShadedRect.Of(ClipRadius + 1, outline: 1));
        else
            _lineBatch.Hide(block.BatchSlot + 2);
    }

    /// <summary>
    ///     What every clip holds, drawn in the tail after the clips: the peak envelope of a wave
    ///     reference, a piano-roll track's notes in miniature, a faithful track's beats. The
    ///     tail starts wherever the clip range currently ends - which is why the leftover clip
    ///     slots are released before this runs - and it grows with the project.
    /// </summary>
    private void DrawContents(float absX, float absY, float width)
    {
        var start = ClipBlockSlot + _blocks.Count * SlotsPerClip;
        var used = 0;
        DrawWaveforms(absX, absY, width, start, ref used);
        _waveBarsDrawn = used;
        DrawMiniatures(absX, absY, width, start, ref used);

        // Released against the previous pass's own range, not this one's: the range moves
        // when the clip count changes, so "everything past what I wrote" would leave the
        // tail of a higher range behind, painted, with no clip under it.
        for (var i = 0; i < _tailSlotsUsed; i++)
        {
            var previous = _tailSlotStart + i;
            if (previous >= start && previous < start + used) continue;
            _lineBatch.Hide(previous);
        }

        _tailSlotStart = start;
        _tailSlotsUsed = used;
    }

    /// <summary>
    ///     The peak envelope inside every wave reference clip, as thin bars centred in the
    ///     clip's body - what makes a reference alignable by eye rather than by ear.
    ///     Nothing is drawn until the file has decoded (<see cref="WavePeaks" /> answers null
    ///     until then), and the bars are clipped to the visible width, so a long reference
    ///     costs slots for the part of it that is on screen.
    /// </summary>
    private void DrawWaveforms(float absX, float absY, float width, int start, ref int used)
    {
        if (WavePeaks is null) return;

        foreach (var (track, tile) in _waveClips)
        {
            if (WavePeaks(track.Path) is not { Length: > 0 } peaks) continue;
            var (clipX, clipY, clipWidth) = (tile.X, tile.Y, tile.Z);
            if (clipWidth <= 0) continue;

            // A lane scrolled off the top or the bottom costs nothing: its bars would be
            // built, uploaded and then scissored away by the batch's clip rect.
            if (clipY + tile.W <= RulerHeight || clipY >= Computed.Height) continue;

            var middle = clipY + WaveBarTop + WaveBarRoom / 2f;
            var color = Vector4.Lerp(ColorOf(track), Vector4.One, WaveBarLift);

            var first = (int)Math.Max(0, Math.Floor(-clipX / WaveBarPitch));
            for (int bar = first, drawn = 0; drawn < WaveBarBudget; bar++, drawn++)
            {
                var x = clipX + bar * WaveBarPitch;
                if (x >= clipX + clipWidth - WaveBarWidth || x > width) break;

                // The clip is the whole file, so its width maps linearly onto the envelope.
                var bucket = (int)(peaks.Length * ((x - clipX) / clipWidth));
                var peak = peaks[Math.Clamp(bucket, 0, peaks.Length - 1)];

                // A bar even where it is silent: the line reads as "the file runs here".
                var height = Math.Max(1f, peak * WaveBarRoom);
                _lineBatch.Set(start + used++, absX + x, absY + middle - height / 2f, WaveBarWidth, height, color);
            }
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
        // just to the view: sibling order only stacks within one Index layer, and deeper
        // layers (Panel.Index cascades +1 per level) render after shallower ones, so
        // nothing here can be kept off the ruler by paint order alone.
        var lanes = IntersectClip(new Vector4i(x, y + (int)RulerHeight, right, bottom), clip);

        foreach (var child in Children) child.ApplyClip(lanes);
        _rulerBackground.ApplyClip(own);

        Background?.ClipRect = clip;
        _lineBatch.ClipRect = lanes;
        _barLabels.ClipRect = own;
        // A clip's name is confined to the lane grid, same as the clip itself, so a clip
        // scrolled up into the ruler band never paints its name over the bar numbers.
        _clipLabels.ClipRect = lanes;
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        ctx.QueueRender(_lineBatch, Index);
        // Past every child, so the captions paint over both the ruler background and the
        // clip fills.
        ctx.QueueRender(_barLabels, LabelLayer);
        ctx.QueueRender(_clipLabels, LabelLayer);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        Context.DequeueRender(_lineBatch, Index);
        Context.DequeueRender(_barLabels, LabelLayer);
        Context.DequeueRender(_clipLabels, LabelLayer);
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);

        if (_marquee.Active && uiContext.CapturedElement != this)
        {
            CommitMarquee();
            _marquee.End();
            InvalidateLayout();
        }

        if (_dragging == null || uiContext.CapturedElement == _dragging) return;

        // The drag ended (release or capture theft): run the rebuild Refresh skipped.
        // A plain click (press with no model change) skipped nothing - and must not
        // rebuild, or the second press of a double-click lands on a fresh ClipBlock
        // and UIContext's same-element check can never see a double-press.
        _dragging = null;
        _clipDrag = null;
        if (!_refreshDeferred) return;
        _refreshDeferred = false;
        Refresh();
    }
}