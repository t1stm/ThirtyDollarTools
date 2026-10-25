// Styles used only by the instrument editor (InstrumentEditor.cs, SoundRow.cs), so they
// live next to them rather than in Scenes/Styles. The layout and its metrics are artboard
// A3 of the design canvas linked from docs/handover/editor-instrument-builder-plan.md.
//
// Two things the design's CSS does that Sundex can't, and how they are stood in for:
// - Padding is one value on all four sides. Where A3 pads unevenly, a fixed height with a
//   larger padding and centred content gives the narrow side (the key chips), or an empty
//   lead/tail panel gives the extra side (a sound row, the Preview button).
// - A corner radius rounds all four corners. The column heads' band, rounded on top only,
//   covers its bottom corners with a square panel.
//
// Widths that have to agree across elements are noted where they are set: the column
// heads above the sound list must line up with every row's read-outs below it.
import "Scenes/Styles/Theme.snx.ss" as theme;

// The minimum window's size: it covers all of a 1080 x 720 window and sits centred in a
// larger one.
// Sized by InstrumentEditor.FitTo: 1080 x 720, less whatever keeps it 24 from every window
// edge - at the 1080 x 720 minimum window it shrinks rather than touching them.
id instrument-editor {
    padding = 16;
    spacing = 14;
}

// ---------------------------------------------------------------- header

class instrument-editor-header {
    width = 100%;
    height = 40;
    spacing = 12;
    vertical-align = "center";
}

class instrument-editor-name-label {
    font-size = 15;
    font-color = $theme.text_dim;
}

// Larger than the other fields: 36 tall, 16 px text 8 in from the left, a 6 radius.
id instrument-name-input {
    width = 320;
    height = 36;
    padding = 8;
    font-size = 16;
    border-radius = 6;
}

// The Preview button is the shared light button with a key (Controls.snx.ss); only its label,
// a separate element because the button is a panel, is styled here.
class instrument-preview-label {
    font-size = 14;
    font-color = $theme.text_dark;
}

// ---------------------------------------------------------------- columns

// What is left between the header and the footer.
class instrument-editor-body {
    width = 100%;
    height = 100%;
    spacing = 14;
}

class instrument-editor-left {
    direction = "vertical";
    width = 100%;
    height = 100%;
    spacing = 8;
}

// A3 pads this 14, with the "All sounds" card pulled out 6 into it so the card's hover fill
// has a margin. Sundex has no negative margins, so the column pads 8 and everything in it
// pads 6 back in. The 13 gaps plus those 6 insets are A3's 19 between blocks.
class instrument-editor-right {
    direction = "vertical";
    width = 300;
    height = 100%;
    padding = 8;
    spacing = 13;
    horizontal-align = "center";
    border-radius = 6;
    background = $theme.background;
}

// The All sounds card's heading: the title, then the sound count in a chip.
class instrument-all-header {
    spacing = 8;
    vertical-align = "center";
}

class instrument-section-title {
    font-size = 16;
    font-color = $theme.header;
}

class instrument-hint-label {
    font-size = 14;
    font-color = $theme.text_dim;
}

// ---------------------------------------------------------------- sound list

// The heading row and the list under it, with no gap so the column heads sit on the rows.
class instrument-sounds-block {
    direction = "vertical";
    width = 100%;
}

class instrument-sounds-header {
    width = 100%;
    height = 38;
    vertical-align = "center";
}

// The column heads' band, a pill in the heading row: the read-out band's 5% white over the
// dialog's fill, made opaque. Same width as a row's read-outs: 2 x 6 padding + 4 x 70 cells
// + 3 x 10 gaps = 322.
class instrument-column-band {
    width = 322;
    height = 32;
    border-radius = 6;
    background = "#222229";
}

class instrument-column-heads {
    width = 322;
    height = 32;
    padding = 6;
    spacing = 10;
    vertical-align = "center";
}

// 70 wide like the cells under it (A3's 60 fits its 8 px heads, not these 9/10 px ones; the
// widest, "Ctrl Volume", is 64), its chip and name centred in it.
class instrument-column-head {
    width = 70;
    spacing = 3;
    horizontal-align = "center";
    vertical-align = "center";
}

class instrument-column-name {
    font-size = 10;
    font-color = $theme.text_dim;
}

class instrument-column-name-lit {
    font-color = $theme.header;
}

// The heads' chips are smaller than the footer's.
class instrument-column-chip-label {
    font-size = 9;
}

// Right of the heads: what lies right of a row's read-outs (102: three 28 buttons, their
// 4 gaps and the row's 6 at the end) plus the list's scroll bar inset (8 bar + 6 gutter).
class instrument-sounds-header-tail {
    width = 116;
    height = 1;
}

// A scroll key, lit while it is the gesture the held keys pick. 1 x 6 padding.
class scroll-chip {
    height = 16;
    padding = 6;
    vertical-align = "center";
    border-radius = 3;
    background = $theme.input_background;
}

class scroll-chip-lit {
    background = $theme.accent;
}

class scroll-chip-label {
    font-size = 10;
    font-color = $theme.text_dim;
}

class scroll-chip-label-lit {
    font-color = "#ffffff";
}

// Its height is set in code: 46 per row, up to six rows, 4 between them.
id instrument-sounds-list {
    width = 100%;
    spacing = 4;
}

class instrument-sounds-empty {
    width = 100%;
    height = 46;
    spacing = 8;
    horizontal-align = "center";
    vertical-align = "center";
    border-radius = 6;
    background = $theme.background;
}

// One sound. Declares its fill so removing sound-row-selected has something to restore.
// The lead (4) plus the 4 gap puts the icon 8 in; the tail (2) plus the gap ends it 6 in.
class sound-row {
    direction = "horizontal";
    width = 100%;
    height = 46;
    spacing = 4;
    vertical-align = "center";
    border-radius = 6;
    background = $theme.background;

    state[hovered] = {
        background = $theme.surface;
    }
}

class sound-row-selected {
    background = $theme.row_selected;

    state[hovered] = {
        background = $theme.row_selected;
    }
}

class sound-row-lead {
    width = 4;
    height = 1;
}

class sound-row-tail {
    width = 2;
    height = 1;
}

// The icon and name: click to select. The wheel passes through it to the list.
class sound-row-select {
    direction = "horizontal";
    width = 100%;
    height = 46;
    spacing = 10;
    vertical-align = "center";
}

class sound-row-name {
    width = 100%;
    height = 34;
    vertical-align = "center";
}

class sound-row-name-label {
    font-size = 14;
    font-color = $theme.text;
}

// The read-outs' band and scroll zone, lighter than the row, and lighter again while the
// pointer is over it. Full row height; 6 at the sides.
// No fill of its own until hovered: each read-out carries a faint one (sound-row-cell), so
// the rows don't stack into one tall band under the heads.
class sound-row-zone {
    direction = "horizontal";
    height = 46;
    padding = 6;
    spacing = 10;
    vertical-align = "center";
    background = "#00000000";
}

class sound-row-zone-hovered {
    background = $theme.scroll_zone_hover;
}

// 3 x 5 padding around 13 px text. 70 wide, like the heads above, and centred like them, so
// a value, the cell lit for it and its column's head share one centre line.
class sound-row-cell {
    width = 70;
    height = 22;
    padding = 5;
    horizontal-align = "center";
    vertical-align = "center";
    border-radius = 4;
    background = $theme.scroll_zone;
}

// The cell a scroll here would change.
class sound-row-cell-target {
    background = $theme.scroll_target;
}

class sound-row-cell-label {
    font-size = 13;
    font-color = $theme.text;
}

// A field at its default reads as a dimmed dash.
class sound-row-cell-default {
    font-color = $theme.text_muted;
}

// ---------------------------------------------------------------- add a sound

// 34 tall in A3, 4 below the list's 8 gap: 38 with its contents at the bottom.
class instrument-add-header {
    width = 100%;
    height = 38;
    spacing = 12;
    vertical-align = "end";
}

// 10 at the sides; the contents are centred, so the same 10 at the top is harmless.

// A3 pads the grid 10. A scroll view also insets its content 6 from the left for its bar,
// so 4 here and 4 on the picker put the icons 14 in from the left and 8 from the top.
id instrument-available {
    width = 100%;
    height = 100%;
    padding = 4;
    border-radius = 6;
    background = $theme.grid_background;
}

id instrument-available-picker {
    width = 100%;
    padding = 4;
}

// ---------------------------------------------------------------- right column

// The instrument-wide fields, and the scroll zone for them; it lights up under the pointer.
class instrument-all-card {
    direction = "vertical";
    width = 100%;
    padding = 6;
    spacing = 2;
    border-radius = 6;
    background = "#00000000";

    state[hovered] = {
        background = "#212433";
    }
}

// The field rows, on top of InspectorForm's form-row / field-label / inspector-field (the
// field's look itself is every field's, Controls.snx.ss): 36 tall, a 70 label and a 10 gap,
// the field filling the 272 the column leaves.
class instrument-form-row {
    height = 36;
}

class instrument-form-label {
    y = 10;
}

class instrument-form-field {
    x = 80;
    y = 2;
    width = 192;
}

// The field a scroll would change, picked out against the others.
class instrument-field-target {
    background = $theme.scroll_target;
}

// The hairline between the instrument's fields and the sound's, inset like they are.
class instrument-rule {
    width = 272;
}

class instrument-no-selection {
    width = 100%;
    padding = 6;
}

// A3 spreads the sound's block down the column (space-between); these gaps are what that
// comes to with a sound selected.
class instrument-sound-section {
    direction = "vertical";
    width = 100%;
    padding = 6;
    spacing = 13;
}

class instrument-sound-fields {
    direction = "vertical";
    width = 100%;
    spacing = 7;
}

class instrument-sound-header {
    width = 100%;
    height = 34;
    spacing = 10;
    vertical-align = "center";
}

class instrument-sound-header-text {
    direction = "vertical";
}

// What the selected sound plays as on a note at 0, one tile per field. 25% each of what the
// 3 gaps leave.
class instrument-results {
    width = 100%;
    spacing = 6;
}

class instrument-result {
    direction = "vertical";
    width = 25%;
    padding = 6;
    border-radius = 4;
    background = $theme.surface;
}

class instrument-result-name {
    font-size = 10;
    font-color = $theme.text_dim;
}

class instrument-result-value {
    font-size = 14;
    font-color = $theme.text;
}

// ---------------------------------------------------------------- footer

class instrument-editor-footer {
    width = 100%;
    height = 36;
    spacing = 6;
    vertical-align = "center";
}

class instrument-footer-label {
    font-size = 12;
    font-color = $theme.text_dim;
}
