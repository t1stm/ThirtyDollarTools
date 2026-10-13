// Frames and rows shared by every modal form in Scenes/Dialogs. The buttons inside
// them come from Controls.snx.ss; this sheet only shapes the boxes those buttons sit in.
//
// Per the rule in Controls.snx.ss, a dialog that wants different padding/spacing than
// `dialog-frame` puts it on its own id rather than adding a second class - ids are
// applied after classes, so the override is deterministic.
import "Scenes/Styles/Theme.snx.ss" as theme;

// The panel a dialog's contents stack in: 16 in, 14 between blocks, a 10 radius. Width is
// per-dialog (below) because it is driven by the content: a two-column import dialog needs far
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

// ---------------------------------------------------------------- per dialog

id confirm-dialog {
    width = 360;
}

id alert-dialog {
    width = 360;
}

id action-value-dialog {
    width = 360;
}

// The whole event as text.
id value-input {
    width = 100%;
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

id track-context-menu {
    width = 280;
    spacing = 12;
}

// The new name spans the menu's width.
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

id import-dialog {
    width = 560;
    padding = 16;
    spacing = 18;
}

// One option of the import dialog: its button with a short explanation underneath.
class dialog-column {
    direction = "vertical";
    width = 240;
    spacing = 6;
}

// The stack of description lines under an option's button.
class dialog-column-description {
    direction = "vertical";
    width = 100%;
    spacing = 2;
}

// The vertical rule between the two options. Tall enough to span a button plus its
// two-line description, so it reads as separating the whole column.
class dialog-divider {
    width = 1;
    height = 84;
    background = $theme.divider;
}

// The row the two options sit in.
class dialog-options {
    width = 100%;
    spacing = 20;
    horizontal-align = "center";
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

// "Or drop a .tdw or .wav file on the window." under the list.
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

// ---------------------------------------------------------------- import warnings

// Four 88-wide cells plus three 8 gaps fit the 388 inner width.
id import-warnings-dialog {
    width = 420;
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

// Up to four 28 icons and their 2 gaps, fixed so every name starts at the same x.
class instrument-row-icons {
    width = 118;
    height = 28;
    spacing = 2;
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
    padding = 10;
    spacing = 8;
}

id sound-filter-list {
    width = 640;
    height = 440;
}

id sound-filter-picker {
    width = 640;
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
