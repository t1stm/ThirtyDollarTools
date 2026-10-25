// Frames and rows shared by every modal form in Scenes/Dialogs. The buttons inside
// them come from Controls.snx.ss; this sheet only shapes the boxes those buttons sit in.
//
// Per the rule in Controls.snx.ss, a dialog that wants different padding/spacing than
// `dialog-frame` puts it on its own id rather than adding a second class - ids are
// applied after classes, so the override is deterministic.
import "Scenes/Styles/Theme.snx.ss" as theme;

// The panel a dialog's contents stack in: 16 in, 14 between blocks, a 10 radius. Width is
// per-dialog (below) because it is driven by the content: a list beside a preview card needs far
// more room than a yes/no confirm.
class dialog-frame {
    direction = "vertical";
    padding = 16;
    spacing = 14;
    border-radius = 10;
    background = $theme.panel;
}

// A dialog's first row: its title, maybe a fact beside it, then a close icon-button flush
// right behind a spacer.
class dialog-header {
    width = 100%;
    height = 28;
    spacing = 10;
    vertical-align = "center";
}

class dialog-title {
    font-size = 16;
    font-color = $theme.header;
}

// What the dialog is about, beside its title (the export's project name).
class dialog-subtitle {
    font-size = 13;
    font-color = $theme.text_dim;
}

// The trailing row holding a dialog's action buttons, right-aligned.
class dialog-actions {
    width = 100%;
    height = 34;
    spacing = 8;
    horizontal-align = "end";
    vertical-align = "center";
}

// Kept as its own name for the dialogs that use it; it is the same row now.
class dialog-actions-compact {
    width = 100%;
    height = 34;
    spacing = 8;
    horizontal-align = "end";
    vertical-align = "center";
}

// An action row with a spacer in it: some buttons flush left, the rest flush right.
// Deliberately unaligned - the spacer does the work.
class dialog-actions-split {
    width = 100%;
    height = 34;
    spacing = 8;
    vertical-align = "center";
}

// A plain left-to-right row inside a dialog (a label with a field after it).
class dialog-row {
    width = 100%;
    spacing = 8;
    vertical-align = "center";
}

// A fixed-height row that positions its children at explicit offsets (label at x=0,
// input at a fixed label width) rather than flowing them.
class form-row {
    width = 100%;
    height = 34;
}

// A form row positions its two children absolutely rather than flowing them: the label
// sits left, the input starts at a fixed column. Both were code-set offsets while the
// export form was hand-built; they are rules now that its tree is markup.
class form-row-label {
    y = 9;
}

class form-row-input {
    x = 190;
}

// A search field: the magnifier, then the text. The instrument editor's and the sound
// filter's sound filters.
class filter-field {
    width = 230;
    height = 32;
    padding = 10;
    spacing = 8;
    vertical-align = "center";
    border-radius = 6;
    background = $theme.input_background;
}

class filter-field-icon {
    font-size = 14;
    font-color = $theme.text_dim;
}

class filter-field-input {
    width = 100%;
    height = 24;
    padding = 2;
    font-size = 14;
    placeholder-color = $theme.text_muted;
    background = "#00000000";
}

// What a list shows while it is empty (EmptyState.cs): an icon and one line, centred in the
// room the list would take. Side by side for a row-high strip, stacked for a pane.
class empty-state {
    width = 100%;
    height = 100%;
    spacing = 8;
    horizontal-align = "center";
    vertical-align = "center";
}

class empty-state-stacked {
    direction = "vertical";
    spacing = 10;
}

class empty-state-icon {
    font-size = 16;
    font-color = $theme.text_subtle;
}

class empty-state-icon-large {
    font-size = 28;
}

class empty-state-text {
    font-size = 13;
    font-color = $theme.text_dim;
}

// ---------------------------------------------------------------- per dialog

id confirm-dialog {
    width = 360;
}

id alert-dialog {
    width = 420;
}

// A failure, not a notice: the triangle Import's warnings use, in the danger red.
class alert-icon {
    font-size = 16;
    font-color = $theme.danger;
}

// The title takes the header's free space and clips there, so a long file name stops short
// of the ×.
class alert-title {
    width = 100%;
    height = 28;
    vertical-align = "center";
}

// The frame's 420 less its 16 padding a side.
class alert-message {
    wrap-width = 388;
}

// The raw error under the message, for a bug report. DialogHost.Details wraps it in code,
// at a width and size that have to match alert-details-text.
class alert-details {
    width = 100%;
    padding = 10;
    border-radius = 6;
    background = $theme.background;
}

class alert-details-text {
    font-size = 12;
    font-color = $theme.text_dim;
}

// Four lines of alert-details-text; more scroll.
class alert-details-scroll {
    width = 100%;
    height = 58;
}

// A faithful action's value: the strip, then a 210 list beside the card. One width for every
// action, so the nine-slot strip fits its 52 tiles and 10 gaps inside the 12 padding.
id action-value-dialog {
    width = 640;
}

class action-description {
    font-size = 13;
    font-color = $theme.text_dim;
}

// The track's slots around the one being added or edited, on the sequence's own canvas shade.
class action-strip {
    width = 100%;
    padding = 12;
    border-radius = 8;
    background = $theme.grid_background;
}

// Its height is set per action by ActionValueDialog.cs - the list and the card both fill it.
class action-body {
    width = 100%;
    spacing = 12;
}

class action-list-column {
    direction = "vertical";
    width = 210;
    height = 100%;
    spacing = 6;
}

class action-list {
    direction = "vertical";
    width = 100%;
    spacing = 3;
}

// A list row: track-kind-row-idle/-selected, swapped by the dialog, give its fill.
class action-row {
    width = 100%;
    height = 36;
    padding = 8;
    spacing = 8;
    vertical-align = "center";
    border-radius = 6;
}

// A color row: ten of them fit beside the card at this height.
class action-row-short {
    height = 30;
}

class action-row-name {
    font-size = 13;
    font-color = $theme.text;
}

class action-row-sub {
    font-size = 12;
    font-color = $theme.text_subtle;
}

// A color row's swatch; its fill is set in code.
class action-row-swatch {
    width = 16;
    height = 16;
    border-radius = 4;
}

class action-card {
    direction = "vertical";
    width = 100%;
    height = 100%;
    padding = 12;
    spacing = 12;
    border-radius = 8;
    background = $theme.background;
}

class action-field {
    direction = "vertical";
    width = 100%;
    spacing = 6;
}

class action-field-input {
    width = 100%;
}

class action-color-row {
    width = 100%;
    spacing = 8;
    vertical-align = "center";
}

// The typed color; its fill is set in code.
class action-color-chip {
    width = 32;
    height = 32;
    border-radius = 4;
}

class action-preview-label {
    font-size = 13;
    font-color = $theme.text_dim;
}

// The !bg / !pulse preview grows into the card's spare height. Its fill is set in code.
class action-preview-box {
    width = 100%;
    height = 100%;
    border-radius = 8;
}

// The "!bg" preview's start color, fade time and end color, under the ramp it shows at rest.
class action-preview-scale {
    width = 100%;
    vertical-align = "center";
}

// Half the free room each, so the fade time sits in the middle; a spacer's 100% each would
// push the end color out of the card.
class action-preview-scale-gap {
    width = 50%;
}

class action-preview-scale-label {
    font-size = 12;
    font-color = $theme.text_dim;
}

// One "!pulse" repeat, along the bottom of the preview at rest.
class action-preview-tick {
    width = 3;
    height = 12;
    border-radius = 1;
    background = $theme.text;
}

// Takes the spare height instead when there is no preview, so the facts sit at the bottom.
class action-fill {
    width = 100%;
    height = 100%;
}

class action-play-button {
    direction = "horizontal";
    height = 28;
    padding = 10;
    spacing = 6;
    font-size = 13;
    border-radius = 6;
    background = $theme.surface;

    state[hovered] = {
        background = $theme.surface_raised;
    }
}

class action-play-icon {
    font-size = 12;
    font-color = $theme.text;
}

// What Save writes, as TDW text.
class action-tdw {
    font-size = 12;
    font-color = $theme.text_subtle;
}

id unsaved-changes-dialog {
    width = 440;
}

// The project being left: its icon, then its name over where it is saved.
class project-card {
    width = 100%;
    padding = 12;
    spacing = 12;
    vertical-align = "center";
    border-radius = 8;
    background = $theme.background;
}

class project-card-icon {
    font-size = 20;
    font-color = $theme.header;
}

// Percent-wide, so the clipped name and path stop at the card's padding.
class project-card-text {
    direction = "vertical";
    width = 100%;
    spacing = 3;
}

class project-card-path {
    font-size = 12;
    font-color = $theme.text_dim;
}

// The recolor grid. Three 88-wide options plus two 8 gaps fit the 288 inner width.
id track-color-dialog {
    width = 320;
    spacing = 12;
}

// Names the colors a mixed selection has, over the grid. The frame's 320 less its padding.
class track-color-caption {
    font-size = 13;
    wrap-width = 288;
}

// How many of a mixed selection's tracks have a color, on its chip's corner.
class color-count {
    height = 16;
    padding = 5;
    horizontal-align = "center";
    vertical-align = "center";
    border-radius = 8;
    background = $theme.text;
}

class color-count-label {
    font-size = 11;
    font-color = $theme.text_dark;
}

class color-grid {
    width = 100%;
    spacing = 8;
    wrap = true;
}

// One swatch: the chip with its name under it. The selected option takes a fill, which
// its padding shows as a ring around the chip - there is no border property to use.
class color-option {
    direction = "vertical";
    width = 88;
    padding = 4;
    spacing = 4;
    horizontal-align = "center";
    border-radius = 6;
}

class color-option-selected {
    background = $theme.row_selected;
}

// A swatch's name. Its own role rather than muted-label: the selected option paints a
// fill behind its name, and text_muted on that shade is barely there.
class color-option-name {
    font-size = 13;
    font-color = $theme.text_dim;
}

class color-option-name-selected {
    font-color = $theme.text;
}

// The color itself. Its fill is set in code - it is a palette entry, not a look.
class color-chip {
    width = 100%;
    height = 34;
    border-radius = 4;
}

id duplicate-track-dialog {
    width = 380;
}

// The new name takes the row's width after its label.
id duplicate-name-input {
    width = 100%;
}

id export-dialog {
    width = 520;
}

// The two formats, side by side: (488 inner - 8) / 2 each.
class export-cards {
    width = 100%;
    spacing = 8;
}

// One format: its name with an icon and the extension, over a line on what it makes. The
// chosen one swaps export-card-idle for export-card-selected - two classes rather than a
// modifier, because a hover override comes from the first class that has one.
class export-card {
    direction = "vertical";
    width = 240;
    padding = 12;
    spacing = 6;
    border-radius = 8;
}

class export-card-idle {
    background = $theme.background;

    state[hovered] = {
        background = $theme.surface;
    }
}

class export-card-selected {
    background = $theme.row_selected;

    state[hovered] = {
        background = $theme.row_selected;
    }
}

class export-card-title {
    spacing = 8;
    vertical-align = "center";
}

class export-card-icon {
    font-size = 16;
    font-color = $theme.header;
}

class export-card-caption {
    font-size = 12;
    font-color = $theme.text_dim;
}

// The chosen format's options. Fixed height (two field rows, the checkbox row and their
// gaps), so picking the other format doesn't resize the dialog.
id export-options {
    direction = "vertical";
    width = 100%;
    height = 116;
    spacing = 4;
}

class export-option-rows {
    direction = "vertical";
    width = 100%;
    spacing = 4;
}

// A field row: the name in a 150 column, the 180-wide field after it, both placed rather
// than flowed (a label's font size resets its width, so a column can't come from width).
class export-row {
    width = 100%;
    height = 36;
}

class export-row-label {
    y = 11;
}

class export-row-input {
    x = 150;
    y = 2;
    width = 180;
}

// A line of icon and text: the checkbox's, the .wav note's.
class export-line {
    width = 100%;
    height = 36;
    spacing = 10;
    vertical-align = "center";
}

class export-line-icon {
    font-size = 15;
    font-color = $theme.text_dim;
}

// A checkbox's own label - smaller than the row text beside it.
class muted-check-label {
    font-size = 13;
}

// The footnote on wave tracks.
class export-note {
    spacing = 8;
    vertical-align = "center";
}

class export-note-icon {
    font-size = 14;
    font-color = $theme.text_subtle;
}

class export-note-label {
    font-size = 12;
    font-color = $theme.text_subtle;
}

// ---------------------------------------------------------------- import

// Add Track's frame and body (below), on its own ids. The card has no color row, so the body
// is 36 shorter: the preview, three facts and the name.
id import-dialog {
    width = 680;
}

id import-body {
    height = 300;
}

// ---------------------------------------------------------------- add a track

id track-type-dialog {
    width = 680;
}

// The kind list, then the picked kind's card. Fixed height, the card's own: the list's gap
// stretches to it, which is what puts the drop hint at the bottom.
class track-kind-body {
    width = 100%;
    height = 336;
    spacing = 12;
}

class track-kind-list {
    direction = "vertical";
    width = 200;
    height = 100%;
    spacing = 4;
}

// One kind: its glyph, its name, then its key flush right. The picked one swaps
// track-kind-row-idle for track-kind-row-selected, as export-card does.
class track-kind-row {
    width = 100%;
    height = 46;
    padding = 12;
    spacing = 10;
    vertical-align = "center";
    border-radius = 6;
}

class track-kind-row-idle {
    background = $theme.background;

    state[hovered] = {
        background = $theme.surface;
    }
}

class track-kind-row-selected {
    background = $theme.row_selected;

    state[hovered] = {
        background = $theme.row_selected;
    }
}

class track-kind-icon {
    font-size = 16;
    font-color = $theme.header;
}

class track-kind-name {
    font-size = 14;
    font-color = $theme.text;
}

class track-kind-gap {
    width = 100%;
    height = 100%;
}

// "Or drop a .tdw or .wav file on the window." under the list; the import dialog's file
// summary in the same place.
class track-kind-drop {
    width = 100%;
    padding = 4;
    spacing = 8;
}

class track-kind-drop-icon {
    font-size = 14;
    font-color = $theme.text_subtle;
}

class track-kind-drop-text {
    direction = "vertical";
    spacing = 3;
}

class track-kind-drop-label {
    font-size = 12;
    font-color = $theme.text_subtle;
}

// The picked kind's card: 436 wide, so 412 inside its padding.
class track-kind-details {
    direction = "vertical";
    width = 100%;
    height = 100%;
    padding = 12;
    spacing = 12;
    border-radius = 8;
    background = $theme.background;
}

// Holds one of TrackKindPreview's pictures, 412 x 156.
class track-kind-preview-slot {
    width = 100%;
    height = 156;
}

class track-kind-facts-slot {
    width = 100%;
}

class track-kind-facts {
    direction = "vertical";
    width = 100%;
    spacing = 6;
}

class track-kind-fact {
    height = 16;
    spacing = 10;
    vertical-align = "center";
}

class track-kind-fact-dot {
    width = 4;
    height = 4;
    border-radius = 2;
    background = $theme.text_muted;
}

class track-kind-fact-label {
    font-size = 13;
    font-color = $theme.text_dim;
}

// The import dialog's glyph in place of the dot: a check, or a warning in yellow (changed on
// the way in) or red (lost). The colors are modifiers listed after it.
class track-kind-fact-icon {
    font-size = 14;
    font-color = $theme.header;
}

class track-kind-fact-warn {
    font-color = $theme.accent_yellow;
}

class track-kind-fact-bad {
    font-color = $theme.danger;
}

class track-kind-fields {
    direction = "vertical";
    width = 100%;
    spacing = 8;
}

class track-kind-name-slot {
    width = 100%;
}

// A field row: the name in a 64 column, then the field, both placed rather than flowed (see
// export-row).
class track-field-row {
    width = 100%;
    height = 32;
}

class track-field-label {
    y = 9;
}

class track-field-input {
    x = 64;
    width = 348;
}

class track-field-note {
    x = 64;
    y = 9;
    font-size = 13;
    font-color = $theme.text_subtle;
}

class track-swatches {
    x = 64;
    y = 5;
    spacing = 6;
}

// One color: its fill is a palette entry, set in code (as color-chip's is). The picked one
// holds a check.
class track-swatch {
    width = 22;
    height = 22;
    border-radius = 4;
    horizontal-align = "center";
    vertical-align = "center";
}

class track-swatch-check {
    font-size = 14;
    font-color = "#ffffff";
}

// The previews (TrackKindPreview.cs): what the track looks like once it is open, on the grid
// canvas's shade. The code places the parts; these only fill them.
class track-preview {
    width = 100%;
    height = 156;
    border-radius = 6;
    background = $theme.grid_background;
}

// Piano roll: the note editor's line work and three of its sound colors.
class track-preview-zero-row {
    background = $theme.zero_row;
}

class track-preview-line {
    background = $theme.step_line;
}

class track-preview-octave {
    background = $theme.octave_line;
}

class track-preview-note {
    border-radius = 2;
    background = $theme.palette_blue;
}

class track-preview-note-purple {
    background = $theme.palette_purple;
}

class track-preview-note-green {
    background = $theme.palette_green;
}

// The import's notes and lanes take their sound's palette color, set in code (as track-swatch's
// is), so these declare no background.
class track-preview-sound-note {
    border-radius = 2;
}

// Drawn under a note the import snaps onto the grid, 1.5 bigger all round: an outline.
class track-preview-snapped {
    border-radius = 3;
    background = $theme.accent_yellow;
}

// Project: one lane per sound, its name beside a clip as long as the file.
class track-preview-lane-name {
    font-size = 11;
    font-color = $theme.text_dim;
}

class track-preview-lane {
    border-radius = 3;
}

// What doesn't fit: "+3" after the instruments, "and 11 more tracks" under the lanes.
class track-preview-more {
    font-size = 10;
    font-color = $theme.text_subtle;
}

// Faithful: the faithful editor at about a third of its size - the Instruments and Actions
// boxes over the Sequence box (faithful-body, faithful-section).
class track-preview-faithful {
    direction = "vertical";
    padding = 6;
    spacing = 6;
}

class track-preview-band {
    width = 100%;
    height = 58;
    spacing = 6;
}

class track-preview-card {
    direction = "vertical";
    height = 100%;
    padding = 5;
    spacing = 4;
    border-radius = 4;
    background = $theme.background;
}

class track-preview-card-instruments {
    width = 52%;
}

class track-preview-card-actions {
    width = 48%;
}

class track-preview-card-sequence {
    width = 100%;
    height = 80;
}

class track-preview-card-bar {
    width = 100%;
    height = 12;
    spacing = 3;
    vertical-align = "center";
}

class track-preview-card-title {
    font-size = 9;
    font-color = $theme.header;
}

// The boxes' header buttons (Modify, Draw, Select), as shapes with their names.
class track-preview-pill {
    height = 12;
    padding = 3;
    vertical-align = "center";
    border-radius = 3;
    background = $theme.divider;
}

class track-preview-pill-on {
    background = $theme.accent;
}

class track-preview-pill-label {
    font-size = 7;
    font-color = $theme.text;
}

class track-preview-icons {
    width = 100%;
    spacing = 3;
    wrap = true;
}

// An instrument's cell in the Instruments box (faithful-palette-cell).
class track-preview-cell {
    padding = 2;
    border-radius = 3;
    background = $theme.surface;
}

// Wave: an arrangement clip with its bars.
class track-preview-clip {
    border-radius = 3;
    background = $theme.accent;
}

// The clip's fill lifted halfway to white, as ArrangementView draws a wave clip's bars
// (WaveBarLift).
class track-preview-wave-bar {
    background = "#a5b5e5";
}

// ---------------------------------------------------------------- cut target

// Three 224 targets and two 8 gaps fill the 688 inner width. Every id here starts "cut-": the
// sheet looks ids up among the class names, so an id like "sound-row" picks up that class too.
id cut-target-dialog {
    width = 720;
}

class cut-modes {
    width = 100%;
    spacing = 8;
}

// A target: its key chip, then its name. The fills are track-kind-row-idle/-selected, swapped
// as one is picked.
class cut-mode {
    width = 224;
    height = 40;
    padding = 10;
    spacing = 10;
    vertical-align = "center";
    border-radius = 6;
}

// The instrument list, then the card of the sounds the cut silences.
class cut-body {
    width = 100%;
    height = 294;
    spacing = 12;
}

class cut-instruments {
    direction = "vertical";
    width = 310;
    height = 100%;
    spacing = 6;
}

class cut-column-label {
    font-size = 12;
    font-color = $theme.text_subtle;
}

// Six and a half 38 rows with their 4 gaps, so a longer list shows it scrolls.
id cut-instrument-list {
    width = 100%;
    height = 272;
    spacing = 4;
}

// One instrument: its sounds' icons, its name and, for a layered one, the count. The fills
// are track-kind-row-idle/-selected, as the targets' are.
class cut-instrument {
    width = 100%;
    height = 38;
    padding = 8;
    spacing = 8;
    vertical-align = "center";
    border-radius = 6;
}

// Up to four 24 icons and their 2 gaps, fixed so every name starts at the same x - the
// instrument selector's instrument-row-icons, a size down.
class cut-instrument-icons {
    width = 102;
    height = 24;
    spacing = 2;
    vertical-align = "center";
}

class cut-instrument-name {
    font-size = 13;
    font-color = $theme.text;
}

// 342 inside its padding, 324 inside the scroll bar's gutters: five 56 tiles with their 10
// margins to a line.
class cut-sounds {
    direction = "vertical";
    width = 100%;
    height = 100%;
    padding = 12;
    spacing = 8;
    border-radius = 8;
    background = $theme.background;
}

// Three lines of tiles and a sliver of the fourth, so a track with many sounds shows it scrolls.
id cut-sound-scroll {
    width = 100%;
    height = 210;
}

// ---------------------------------------------------------------- import warnings

// Four 88-wide cells plus three 8 gaps fit the 388 inner width.
id import-warnings-dialog {
    width = 420;
}

// The frame's 420 less its 16 padding a side.
class warning-footer-text {
    wrap-width = 388;
}

// One kind of warning: its heading, a line on what became of it, then what it applies to.
class warning-section {
    direction = "vertical";
    width = 100%;
    spacing = 6;
}

class warning-grid {
    width = 100%;
    spacing = 8;
    wrap = true;
}

// The faithful palette's cell (Panels.snx.ss) without its hover - these tiles show what
// was left out, there is nothing to click. Wider than the palette's 76 so an event name
// and its count share the caption line.
class warning-cell {
    direction = "vertical";
    horizontal-align = "center";
    width = 88;
    padding = 6;
    spacing = 4;
    border-radius = 6;
    background = $theme.surface;
}

// ---------------------------------------------------------------- instruments

id instrument-selector {
    width = 460;
}

// Five and a half 46 rows with their 4 gaps, so a longer list shows it scrolls.
id instrument-selector-list {
    width = 100%;
    height = 296;
    spacing = 4;
}

// One instrument, a builder sound row's size: click to pick, with Preview/Edit/Delete
// icon-buttons flush right and 6 after them (the tail plus the 4 gap). The active one swaps
// instrument-row-idle for instrument-row-selected, as export-card does.
class instrument-row {
    direction = "horizontal";
    vertical-align = "center";
    width = 100%;
    height = 46;
    spacing = 4;
    border-radius = 6;
}

class instrument-row-idle {
    background = $theme.background;

    state[hovered] = {
        background = $theme.surface;
    }
}

class instrument-row-selected {
    background = $theme.row_selected;

    state[hovered] = {
        background = $theme.row_selected;
    }
}

// The icons and the name. 8 in from the left; the contents are centred, so the 8 at the top
// is harmless.
class instrument-row-pick {
    width = 100%;
    height = 46;
    padding = 8;
    spacing = 12;
    vertical-align = "center";
}

// Two 28 icons and their 4 gap, fixed so every name starts at the same x - close beside the
// icons rather than a four-icon slot away.
class instrument-row-icons {
    width = 60;
    height = 28;
    spacing = 4;
    vertical-align = "center";
}

class instrument-row-text {
    direction = "vertical";
    width = 100%;
    spacing = 2;
}

class instrument-row-name {
    font-size = 14;
    font-color = $theme.text;
}

class instrument-row-count {
    font-size = 11;
    font-color = $theme.text_subtle;
}

class instrument-row-count-selected {
    font-color = $theme.text_dim;
}

class instrument-row-tail {
    width = 2;
    height = 1;
}

class instrument-selector-empty {
    width = 100%;
    height = 296;
    horizontal-align = "center";
    vertical-align = "center";
}

// ---------------------------------------------------------------- sound filter

id sound-filter-frame {
    width = 640;
    spacing = 12;
}

// The picks, one row tall from the start, so the grid under it never moves when one is
// added. More than a row scrolls.
id sound-filter-tray {
    width = 100%;
    height = 58;
    padding = 4;
    border-radius = 8;
    background = $theme.background;
}

// Percent-wide, so the scroll bar sits inside the frame's padding.
id sound-filter-list {
    width = 100%;
    height = 360;
}

id sound-filter-picker {
    width = 100%;
}

// ---------------------------------------------------------------- file dialog

// Sundex's FileSelection, under dialog-frame (DialogHost). Its parts carry these classes; see
// its summary for the list.
id file-dialog {
    width = 640;
    height = 472;
    spacing = 12;
}

// Up, a hairline, then the path as crumbs.
class file-location {
    width = 100%;
    height = 36;
    padding = 4;
    spacing = 2;
    vertical-align = "center";
    border-radius = 6;
    background = $theme.input_background;
}

// The hairline with 6 either side of it.
class file-location-gap {
    width = 13;
    height = 18;
    horizontal-align = "center";
}

class file-location-rule {
    width = 1;
    height = 18;
    background = $theme.divider;
}

class file-crumbs {
    width = 100%;
    height = 28;
    spacing = 2;
    vertical-align = "center";
}

// A folder on the path. Only the ones above the current folder are links (file-crumb-link).
class file-crumb {
    height = 28;
    padding = 8;
    spacing = 6;
    vertical-align = "center";
    border-radius = 6;
    background = "#00000000";
}

class file-crumb-link {
    state[hovered] = {
        background = $theme.divider;
    }
}

class file-crumb-icon {
    font-size = 14;
    font-color = $theme.text_dim;
}

class file-crumb-label {
    font-size = 13;
    font-color = $theme.text_dim;
}

class file-crumb-label-current {
    font-color = $theme.text;
}

class file-crumb-chevron {
    font-size = 12;
    font-color = $theme.text_muted;
}

// The path as text, while it is being typed (click the crumbs, or Ctrl+L). No fill of its own:
// the bar it replaces the crumbs in is the field. 6 in, so the 15 px line fits the 28.
class file-path-input {
    width = 100%;
    height = 28;
    padding = 6;
    font-size = 13;
    background = "#00000000";
}

// A path that leads nowhere, until it is edited.
class file-path-input-invalid {
    font-color = $theme.danger;
}

// The folder's contents, on the grid's darker fill.
class file-list {
    width = 100%;
    height = 100%;
    spacing = 2;
    border-radius = 6;
    background = $theme.grid_background;
}

// 34 tall, 10 at the sides (centred, so the 10 at the top is harmless). The picked file swaps
// file-row-idle for file-row-selected, as export-card does.
class file-row {
    width = 100%;
    height = 34;
    padding = 10;
    spacing = 10;
    vertical-align = "center";
    border-radius = 6;
}

class file-row-idle {
    background = "#00000000";

    state[hovered] = {
        background = $theme.surface;
    }
}

class file-row-selected {
    background = $theme.row_selected;

    state[hovered] = {
        background = $theme.row_selected;
    }
}

class file-row-folder {
    font-size = 16;
    font-color = $theme.header;
}

class file-row-icon {
    font-size = 16;
    font-color = $theme.text_dim;
}

class file-row-name {
    font-size = 14;
    font-color = $theme.text;
}

class file-row-date {
    font-size = 12;
    font-color = $theme.text_subtle;
}

class file-list-empty {
    width = 100%;
    height = 100%;
    horizontal-align = "center";
    vertical-align = "center";
}

class file-list-empty-label {
    font-size = 14;
    font-color = $theme.text_dim;
}

// A line under the folders when none of the files match.
class file-list-note {
    width = 100%;
    height = 30;
    padding = 10;
    vertical-align = "center";
}

class file-list-note-label {
    font-size = 13;
    font-color = $theme.text_subtle;
}

// Shown over the footer while the typed name is a file that is already there.
class file-overwrite {
    width = 100%;
    height = 18;
    spacing = 8;
    vertical-align = "center";
}

class file-overwrite-icon {
    font-size = 15;
    font-color = $theme.accent_yellow;
}

class file-overwrite-label {
    font-size = 13;
    font-color = $theme.accent_yellow;
}

class file-footer {
    width = 100%;
    height = 34;
    spacing = 8;
    vertical-align = "center";
}

class file-name-input {
    width = 240;
}

class file-ext-label {
    font-size = 13;
    font-color = $theme.text_subtle;
}
