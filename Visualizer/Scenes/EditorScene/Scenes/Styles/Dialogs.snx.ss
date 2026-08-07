// Frames and rows shared by every modal form in Scenes/Dialogs. The buttons inside
// them come from Controls.snx.ss; this sheet only shapes the boxes those buttons sit in.
//
// Per the rule in Controls.snx.ss, a dialog that wants different padding/spacing than
// `dialog-frame` puts it on its own id rather than adding a second class - ids are
// applied after classes, so the override is deterministic.
import "Scenes/Styles/Theme.snx.ss" as theme;

// The panel a dialog's contents stack in. Width is per-dialog (below) because it is
// driven by the content: a two-column import dialog needs far more room than a
// yes/no confirm.
class dialog-frame {
    direction = "vertical";
    padding = 14;
    spacing = 14;
    background = $theme.panel;
}

// The trailing row holding a dialog's action buttons, right-aligned.
class dialog-actions {
    width = 100%;
    height = 40;
    spacing = 10;
    horizontal-align = "end";
    vertical-align = "center";
}

// The same row, 36 tall, for dialogs whose buttons are the compact kind.
class dialog-actions-compact {
    width = 100%;
    height = 36;
    spacing = 10;
    horizontal-align = "end";
    vertical-align = "center";
}

// An action row with a spacer in it: some buttons flush left, the rest flush right.
// Deliberately unaligned - the spacer does the work.
class dialog-actions-split {
    width = 100%;
    height = 40;
    spacing = 10;
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

id unsaved-changes-dialog {
    width = 360;
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
    width = 420;
    spacing = 10;
}

// The export dialog's action row is a little taller than the shared split row.
id export-actions {
    height = 44;
}

// The export options' numeric boxes.
class export-field {
    width = 120;
    height = 32;
    font-size = 14;
}

// A checkbox's own label - smaller than the row text beside it.
class muted-check-label {
    font-size = 13;
}

id import-dialog {
    width = 560;
    padding = 16;
    spacing = 18;
}

// One import option: its button with a short explanation underneath.
class import-column {
    direction = "vertical";
    width = 240;
    spacing = 6;
}

// The stack of description lines under an import option's button.
class import-column-description {
    direction = "vertical";
    width = 100%;
    spacing = 2;
}

// The vertical rule between the two import options. Tall enough to span a button plus
// its two-line description, so it reads as separating the whole column.
class import-divider {
    width = 1;
    height = 84;
    background = $theme.divider;
}

// The row the two import options sit in.
class import-options {
    width = 100%;
    spacing = 20;
    horizontal-align = "center";
}

// ---------------------------------------------------------------- instruments

id instrument-editor {
    width = 640;
    padding = 10;
    spacing = 8;
}

id instrument-editor-sounds {
    width = 100%;
    height = 380;
}

id instrument-editor-picker {
    width = 100%;
}

// Preview and Done. Their 8px corner is rounder than a dialog action's 6.
class editor-action-button {
    font-size = 14;
    border-radius = 8;
    background = $theme.accent;

    state[hovered] = {
        background = $theme.accent_hover;
    }
}

class instrument-editor-done-row {
    width = 100%;
    horizontal-align = "end";
}

id instrument-name-input {
    width = 300;
    font-size = 15;
    border-radius = 6;
    background = $theme.input_background;
}

id instrument-selector {
    direction = "vertical";
    width = 320;
    height = 420;
    padding = 10;
    background = $theme.panel;
}

id instrument-selector-list {
    width = 100%;
    height = 100%;
    spacing = 4;
}

// One instrument row: click to pick, with Edit/Delete buttons flush right.
class instrument-row {
    direction = "horizontal";
    vertical-align = "center";
    width = 100%;
    height = 36;
    padding = 6;
    spacing = 10;
    border-radius = 6;
    background = $theme.panel;
}

// The Edit/Delete buttons on an instrument row - smaller than a dialog action.
// Standalone rather than dialog-button-light/-danger plus a size class: they'd both
// set font-size, which the rule at the top of Controls.snx.ss forbids.
class row-button-light {
    font-size = 12;
    border-radius = 6;
    background = $theme.header;

    state[hovered] = {
        background = $theme.header_hover;
    }
}

class row-button-danger {
    font-size = 12;
    border-radius = 6;
    background = $theme.danger;

    state[hovered] = {
        background = $theme.danger_hover;
    }
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

id file-dialog {
    width = 560;
    height = 440;
}
