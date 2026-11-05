// Frames and rows shared by every modal form, the editor's and Home's. The buttons inside
// them come from Controls.snx.ss; this sheet only shapes the boxes those buttons sit in. Rules
// for one dialog stay with that dialog's scene.
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

// A title that can carry a long name (the alert's file, the confirm's instrument): it takes the
// header's free space and clips there, so the name stops short of the ×.
class dialog-title-clip {
    width = 100%;
    height = 28;
    vertical-align = "center";
}

// A card on the dialog's fill: the project being left, the build you are on.
class project-card {
    width = 100%;
    padding = 12;
    spacing = 12;
    vertical-align = "center";
    border-radius = 8;
    background = $theme.background;
}
