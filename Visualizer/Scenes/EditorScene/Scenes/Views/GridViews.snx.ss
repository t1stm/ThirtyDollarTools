// Styling for the two canvas views - the arrangement and the note editor - kept next
// to ArrangementView.cs/TrackEditorView.cs because nothing else uses it.
//
// Most of a grid is painted from a LineBatch and pools of reassigned planes, not from
// styled components, so there is no element for a selector to hit. Those colors are
// [NamedSetting] properties on the view itself instead - the two canvas rules below -
// and the draw code reads them off `this`. Everything that IS an element (the gutter,
// the strip, a clip block) is styled by its own class like anything else.
import "Scenes/Styles/Theme.snx.ss" as theme;

// ---------------------------------------------------------------- the canvases

// The arrangement grid. Sits next to grid-view (the box) on <arrangement-view/>.
class arrangement-canvas {
    background = $theme.grid_background;

    // Lane dividers and bar lines, batched into one instanced draw call.
    line-color = $theme.surface;

    // The bar ruler's numbers, and the brighter shade the bar under the playhead takes.
    label-color = $theme.text_dim;
    playhead-color = $theme.playhead;

    // A clip's fill, batched with the lines, the shade it takes while selected, and the
    // track name written across it.
    clip-color = $theme.accent;
    clip-selected-color = $theme.selection_highlight;
    clip-label-color = $theme.text;
}

// The note editor's grid. Sits next to grid-view on <track-editor-view/>.
class note-canvas {
    background = $theme.grid_background;

    // Beat numbers on the ruler, and the beat the playhead is in.
    label-color = $theme.text_dim;
    playhead-color = $theme.playhead;

    // The line work, faintest first: every step, the row lines, the octave (every
    // 12th) emphasis, the beat emphasis, and the segment boundaries.
    step-line-color = $theme.step_line;
    row-line-color = $theme.row_line;
    octave-line-color = $theme.octave_line;
    beat-line-color = $theme.surface;
    boundary-color = $theme.text_muted;

    // The !cut row pinned under the grid, the band behind value 0, and the bands the
    // segment strip and beat ruler sit on.
    cut-row-color = $theme.cut_row;
    zero-row-color = $theme.zero_row;
    strip-color = $theme.panel;

    // Segment strips alternate between the first two so boundaries are visible without
    // a separating line; the selected segment takes the accent.
    strip-segment-a = $theme.strip_segment_a;
    strip-segment-b = $theme.strip_segment_b;
    strip-selected-color = $theme.accent;

    // A note block's fill: its instrument's entry in the palette, or the highlight when
    // it is part of the selection.
    selected-note-color = $theme.selection_highlight;
    sound-palette = $theme.sound_palette;
}

// ---------------------------------------------------------------- grid furniture

// The pooled ghost panels both views park and resize every layout. They are ordinary
// children, so a class is all they need - the view no longer passes a color in.

// The value gutter down the left of the note editor. The note editor's own strip/ruler
// bands are batch slots (see note-canvas above); this is the arrangement's ruler.
class grid-gutter {
    background = $theme.panel;
}

class grid-strip {
    background = $theme.panel;
}

// The rule separating the pinned !cut row from the grid.
class grid-cut-rule {
    background = $theme.text_muted;
}

// One playback position line. Pooled: a project can be playing several tracks at once.
class grid-playhead {
    background = $theme.playhead;
}

// The drag-select rectangle. Translucent (the trailing 40 is its alpha) so the notes
// and clips underneath stay readable while the marquee is over them.
class grid-marquee {
    background = $theme.marquee_fill;
}

// ---------------------------------------------------------------- clips

// A clip on an arrangement lane has no rule of its own any more: its fill, its selected
// shade and its name all come from arrangement-canvas's settings above, written into the
// batches. The name's inset and size live next to that code (ClipPadding, ClipFontSize).
