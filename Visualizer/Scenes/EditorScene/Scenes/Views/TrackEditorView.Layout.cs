using EditorScene.Scenes.Components;
using EditorScene.State;
using OpenTK.Mathematics;
using Sundex.Components.Labels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The layout pass: reassigns the pooled note, strip, keyframe and label slots to whatever
///     the viewport shows, places the playheads (following them during playback) and the
///     marquee, and caches the per-track scans that pass needs.
/// </summary>
public partial class TrackEditorView
{
    // Cached longest-note scan, keyed by the track and the edit revision it was taken at.
    private int _longestNoteRevision = -1;
    private float _longestNoteSteps = 1;
    private ProjectTrack? _longestNoteTrack;

    // How many pool blocks from the front may still be placed from the last layout.
    private int _noteBlocksPlaced;

    // Cached cut harvest for the resume marks, keyed the same way.
    private IReadOnlyList<CutPoint>? _cutPoints;
    private int _cutPointsRevision = -1;
    private ProjectTrack? _cutPointsTrack;

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
            // Line r sits on top of row r, so it's the bottom edge of the row above it:
            // the octave line runs under each multiple of 12 (11|12, -1|0, -13|-12).
            var color = (MaxValue - r + 1) % 12 == 0 ? OctaveLineColor : RowLineColor;
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

            _automationPath.BeginFrame();
            var longestNoteSteps = LongestNoteSteps(track);
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
                // A long note whose segment ends left of the viewport can still reach into it.
                if (offset + longestNoteSteps * pps < visibleStart || segWidth <= 0) continue;

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
                    var x = segStart + note.Step * pps;
                    var noteWidth = NoteSteps(track, segment, note) * pps;
                    if (note.Automation != null && x <= visibleEnd && x + noteWidth >= visibleStart)
                    {
                        // The path paints ON the note body: a lightened instrument colour over
                        // its fill, and the ring's colour over a selected one - its lifted body
                        // would swallow the plain instrument colour.
                        var selected = _state.SelectedNotes.Contains(note);
                        _automationPath.Draw(_geometry, (absX, absY), track, segment, note, segStart,
                            NoteSteps(track, segment, note),
                            new AutomationPath.Look(NoteColor(note, selected), selected,
                                selected ? SelectionRingColor : Lighten(InstrumentColor(note.Instrument), 0.45f),
                                SelectionRingColor, AutomationEndColor),
                            Shading, ref autoMark, CutPoints(track));
                    }

                    if (_dragging?.Note == note) continue;
                    if (x + noteWidth < visibleStart || x > visibleEnd) continue;
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

        // A released block gives up both its hit box and its slots - only up to the last
        // layout's high-water mark, past which nothing was placed to release.
        for (var i = noteBlock; i < _noteBlocksPlaced; i++)
        {
            if (_noteBlocks[i] == _dragging) continue;
            Hide(_noteBlocks[i]);
            for (var slot = 0; slot < SlotsPerNote; slot++) _blockBatch.Hide(NoteBlockSlot + i * SlotsPerNote + slot);
        }

        _noteBlocksPlaced = _dragging is { } pinned ? Math.Max(noteBlock, _noteBlocks.IndexOf(pinned) + 1) : noteBlock;

        _automationPath.HideUnused(autoMark);
        PlaceKeyframeBlocks();
        for (var i = stepLine; i < StepLinePool; i++) _lineBatch.Hide(StepLineSlot + i);
        for (var i = stripBlock; i < _stripBlocks.Count; i++)
        {
            Hide(_stripBlocks[i]);
            _blockBatch.Hide(StripBlockSlot + i);
        }

        for (var i = boundary; i < BoundaryLinePool; i++) _lineBatch.Hide(BoundaryLineSlot + i);
        for (var i = beatLabel; i < BeatLabelPool; i++) _labelBatch.Hide(BeatLabelSlot + i);

        // The bands the strip and ruler sit on: batch slots after every note's, so a note
        // scrolled above the grid is masked by them.
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
        // The marquee's axes are (step, value); a box dragged into the cut row stops at the grid.
        _marquee.Layout((step, value) => (GutterWidth + (float)(step * pps) - scrollX, _geometry.ValueTop(value)),
            gridBottom);

        base.DoLayout();
        ApplyClip(_inheritedClip);
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

    /// <summary>
    ///     Lays one note out twice: its hit box is the whole cell span, grid line to grid line,
    ///     so two notes side by side leave no dead pixel between them; its tile is drawn
    ///     centred inside that, clear of the lines (see <see cref="TrackEditorGeometry.CellX" />).
    /// </summary>
    private void PlaceNote(NoteBlock block, TrackSegment segment, Note note, float segStartPx,
        int cutSlot = 0, int cutSlotCount = 1)
    {
        block.Assign(segment, note);
        var stepPx = segStartPx + note.Step * PixelsPerStep;
        float x, y, width, height;
        Vector4 tile;
        if (note.IsCut)
        {
            // Fixed row - never scrolls, unlike every other row. Simultaneous cuts on
            // different instruments (cutSlotCount > 1) split the step evenly instead of
            // stacking exactly on top of each other, each centred in its own slot.
            var slotWidth = PixelsPerStep / cutSlotCount;
            var margin = TrackEditorGeometry.MarginFor(slotWidth);
            x = _geometry.LineX(stepPx) + cutSlot * slotWidth + 1;
            width = Math.Max(1, slotWidth - 1);
            y = _geometry.CutRowTop + 1;
            height = CutRowHeight - 2;
            tile = new Vector4(x + margin, y, Math.Max(2, width - 2 * margin), height);
        }
        else
        {
            var steps = _state.OpenedTrack is { } track ? NoteSteps(track, segment, note) : 1;
            x = _geometry.LineX(stepPx) + 1;
            width = steps * PixelsPerStep - 1;
            y = _geometry.ValueTop(note.Value) + 1;
            // No overdraw covers a grid note bleeding past the grid's bottom edge
            // (unlike the top, still covered by the strip/ruler bands), so clamp it.
            var gridBottom = _geometry.GridBottom;
            height = Math.Max(0, Math.Min(y + _geometry.RowHeight - 1, gridBottom) - y);
            var tileY = _geometry.NoteTop(note.Value);
            tile = new Vector4(_geometry.CellX(stepPx), tileY, _geometry.SpanWidth(steps),
                Math.Max(0, Math.Min(tileY + _geometry.NoteHeight, gridBottom) - tileY));
        }

        // The element carries the hit box; the batch slots carry the tile.
        block.X = x;
        block.Y = y;
        block.Width = width;
        block.Height = height;
        block.Tile = tile;
        DrawNote(block, note, new Vector4(x, y, width, height));
    }

    /// <summary>
    ///     A note's three slots: a drop shadow 1 px straight down, the lit tile filled at its
    ///     volume, and - while it is selected - a ring around the whole cell span, which is
    ///     the margin the tile keeps from the grid lines (or its own edge, with none to spare).
    /// </summary>
    private void DrawNote(NoteBlock block, Note note, Vector4 hit)
    {
        var (absX, absY) = (Computed.AbsoluteX, Computed.AbsoluteY);
        var tile = block.Tile;
        var selected = _state.SelectedNotes.Contains(note);
        var radius = CornerRadius(tile.Z, tile.W);

        if (PixelsPerStep >= MinShadowPixelsPerStep && tile.W > 0)
            _blockBatch.Set(block.BatchSlot - 1, absX + tile.X, absY + tile.Y + 1, tile.Z, tile.W,
                new Vector4(0, 0, 0, ShadowAlpha * Shading), ShadedRect.Of(radius));
        else
            _blockBatch.Hide(block.BatchSlot - 1);

        _blockBatch.Set(block.BatchSlot, absX + tile.X, absY + tile.Y, tile.Z, tile.W,
            VolumeFill(NoteColor(note, selected), note.Volume), ShadedRect.Of(radius, strength: Shading));

        if (selected)
            _blockBatch.Set(block.BatchSlot + 1, absX + hit.X, absY + hit.Y, hit.Z, hit.W, SelectionRingColor,
                ShadedRect.Of(radius + Math.Min(tile.X - hit.X, tile.Y - hit.Y), outline: 1));
        else
            _blockBatch.Hide(block.BatchSlot + 1);
    }

    /// <summary>
    ///     A note's drawn length in grid steps: its automation's end, or one step when it has
    ///     none. Never shorter than a step, so a stub automation still shows a note.
    /// </summary>
    private float NoteSteps(ProjectTrack track, TrackSegment segment, Note note)
    {
        if (note.Automation is not { End: > 0 } automation) return 1;
        return Math.Max(1, automation.EndSteps(segment.StepMinutes(track.Timing.BPM)));
    }

    /// <summary>
    ///     How far the longest note of the track reaches, in steps - how much wider than the
    ///     viewport the segment scan has to start, since a long note in a segment that ends
    ///     off-screen left can still reach into view. Rescanned only when the project changed
    ///     (<see cref="EditorState.Revision" />), not per frame: per frame it would undo the
    ///     culling it exists to keep correct.
    /// </summary>
    private float LongestNoteSteps(ProjectTrack track)
    {
        if (ReferenceEquals(_longestNoteTrack, track) && _longestNoteRevision == _state.Revision)
            return _longestNoteSteps;

        var longest = 1f;
        foreach (var segment in track.Segments)
        foreach (var note in segment.Notes)
            if (note.Automation is { End: > 0 })
                longest = Math.Max(longest, NoteSteps(track, segment, note));

        _longestNoteTrack = track;
        _longestNoteRevision = _state.Revision;
        _longestNoteSteps = longest;
        return longest;
    }

    /// <summary>
    ///     The track's own cuts, for the resume marks: a cut is by sound name, so one note's
    ///     retrigger guard silences every other note playing the same sounds, and auto-resume
    ///     puts them back. Rebuilt only when the project changed, the same way
    ///     <see cref="LongestNoteSteps" /> is - it walks every note of the track.
    /// </summary>
    // ponytail: a drag edits per frame, so this re-expands the track per frame while one
    // runs. It is the same walk the playback re-render already makes on every edit -
    // measure before caching it against anything finer than the revision.
    private IReadOnlyList<CutPoint>? CutPoints(ProjectTrack track)
    {
        if (!_state.Project.AutoResume) return null;
        if (ReferenceEquals(_cutPointsTrack, track) && _cutPointsRevision == _state.Revision) return _cutPoints;

        _cutPointsTrack = track;
        _cutPointsRevision = _state.Revision;
        _cutPoints = track.CutPoints();
        return _cutPoints;
    }

    /// <summary>
    ///     Hangs the marker grab boxes on whatever <see cref="AutomationPath" /> just drew,
    ///     so only the markers actually on screen are catchable. Only a selected note's
    ///     markers get one: they sit on top of the note body, so on every other note they
    ///     would swallow the press that selects it. Reach for a note first, then shape it.
    ///     The block a drag has hold of keeps its keyframe for the whole gesture, the way a
    ///     dragged note keeps its block.
    /// </summary>
    private void PlaceKeyframeBlocks()
    {
        var used = 0;
        foreach (var handle in _automationPath.Handles)
        {
            var pinned = _keyframeDrag is { } drag && drag.Note == handle.Note && drag.KeyframeIndex == handle.Index;
            if (!pinned && !_state.SelectedNotes.Contains(handle.Note)) continue;
            KeyframeBlock block;
            if (pinned)
            {
                block = _keyframeDrag!;
            }
            else
            {
                while (used < _keyframeBlocks.Count && _keyframeBlocks[used] == _keyframeDrag) used++;
                if (used >= _keyframeBlocks.Count) break;
                block = _keyframeBlocks[used++];
            }

            // The marker's own box, grown to the grab radius around its centre where it is
            // smaller, so a 4 px marker stays catchable.
            var width = Math.Max(handle.Width, KeyframeBlock.GrabRadius * 2);
            var height = Math.Max(handle.Height, KeyframeBlock.GrabRadius * 2);
            block.Assign(handle.Note, handle.Segment, handle.Index);
            block.X = handle.X - width / 2;
            block.Y = handle.Y - height / 2;
            block.Width = width;
            block.Height = height;
        }

        for (var i = used; i < _keyframeBlocks.Count; i++)
            if (_keyframeBlocks[i] != _keyframeDrag)
                Hide(_keyframeBlocks[i]);
    }
}
