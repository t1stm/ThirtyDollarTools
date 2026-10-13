// The editor's shared control vocabulary: buttons, text fields and label roles that
// more than one screen uses. Anything used by exactly one component lives in that
// component's own sheet next to its .cs instead.
//
// Code-built elements pick these up the same way markup ones do - a panel passes its
// sheet to whatever is added to it (Sundex.Components.Panels.Panel.AddChild), so
// setting Classes on an element is all it takes.
//
// Classes on one element are applied in the order they are listed - in markup's
// `class="[a,b]"`, or in the C# list - and a later one overrides an earlier one's
// properties; the id is applied after all of them (see UIElement.SetNamedSetting). That
// is what makes a modifier class work: the base rule sets the property, the modifier
// listed after it overrides, and removing the modifier restores the base value.
// Two classes setting the same property by accident still just means the later wins.
import "Scenes/Styles/Theme.snx.ss" as theme;

// ---------------------------------------------------------------- buttons

// Every dialog action is the instrument builder's Done: 34 tall, a radius of 8, 22 at the
// sides (the label is centred, so the 22 at the top is harmless), 14 px. Each fill class
// restates the shape, so any one of them alone makes a whole button.

// The default dialog action (Cancel): the quiet fill, for anything that isn't the main choice.
class dialog-button {
    height = 34;
    padding = 22;
    font-size = 14;
    border-radius = 8;
    background = $theme.divider;

    state[hovered] = {
        background = $theme.divider_hover;
    }
}

// The affirmative action of a dialog (Save, Export, Done).
class dialog-button-primary {
    height = 34;
    padding = 22;
    font-size = 14;
    border-radius = 8;
    background = $theme.accent;

    state[hovered] = {
        background = $theme.accent_hover;
    }
}

// A destructive action that is a dialog's whole point (Delete). Light fill, so its label is
// darkened separately - see the dark-label class below.
class dialog-button-danger {
    height = 34;
    padding = 22;
    font-size = 14;
    border-radius = 8;
    background = $theme.danger;

    state[hovered] = {
        background = $theme.danger_hover;
    }
}

// A destructive way out that must not compete with the main choice (Discard changes): no
// fill until hovered, a red label (danger-label), 14 at the sides. Stands alone at the left.
class dialog-button-danger-quiet {
    height = 34;
    padding = 14;
    font-size = 14;
    border-radius = 8;
    background = "#00000000";

    state[hovered] = {
        background = "#f7768e1f";
    }
}

class danger-label {
    font-color = $theme.danger;
}

// The light-blue action (Duplicate, the builder's Preview). Also needs a dark label.
class dialog-button-light {
    height = 34;
    padding = 22;
    font-size = 14;
    border-radius = 8;
    background = $theme.header;

    state[hovered] = {
        background = $theme.header_hover;
    }
}

// The yellow alternative in a two-option dialog. Dark label as well.
class dialog-button-alt {
    height = 34;
    padding = 22;
    font-size = 14;
    border-radius = 8;
    background = $theme.accent_yellow;

    state[hovered] = {
        background = $theme.accent_yellow_hover;
    }
}

// Height only, listed after a fill: the import dialog's two options must line up across the
// divider between them.
class dialog-button-tall {
    height = 40;
}

// Shape without a fill, for a button that reads as a bare label - the track context
// menu's Cancel, the import dialog's "or as a Piano Roll track". Nothing shows its edges, so
// it pads only 5: its text lines up with the text around it.
class dialog-button-shape {
    height = 34;
    padding = 5;
    font-size = 14;
    border-radius = 8;
}

// A modifier listed after a fill, for a button that leads with its key: the chip, the label,
// then a tail (button-tail). Padding is one value on every side, so 8 on the left, and the
// tail after the 8 gap makes the right 16. KeyChip.Add builds it.
class key-button {
    direction = "horizontal";
    padding = 8;
    spacing = 8;
    horizontal-align = "center";
    vertical-align = "center";
}

class button-tail {
    width = 0;
    height = 1;
}

// 2 x 7 padding: 18 tall with the 11 px key centred, 7 at the sides. Its fill is 18% of the
// darkest text over whatever button it sits on.
class key-chip {
    height = 18;
    padding = 7;
    vertical-align = "center";
    border-radius = 4;
    background = $theme.key_label;
}

// Listed after key-chip, for a chip on a card or row rather than a button: a light veil, so it
// shows on the row's idle, hovered and selected fills alike.
class key-chip-on-card {
    background = "#ffffff14";
}

// The key's name: dark on a light button, and the two below on the others.
class key-chip-label {
    font-size = 11;
    font-color = $theme.text_dark;
}

class key-chip-label-on-accent {
    font-color = "#ffffffd9";
}

class key-chip-label-on-quiet {
    font-color = $theme.text_dim;
}

// The ghost button holding one Lucide glyph (a row's play/edit/delete, a dialog's close):
// 28 square, no fill until hovered.
class icon-button {
    width = 28;
    height = 28;
    padding = 0;
    border-radius = 6;
    background = "#00000000";

    state[hovered] = {
        background = $theme.divider;
    }
}

class icon-button-icon {
    font-size = 16;
    font-color = $theme.text_dim;
}

// The play triangle is the smaller of the glyphs.
class icon-button-play {
    font-size = 14;
}

// A small fact beside a title (a file type, a count): the key chip's shape on the field fill.
class info-chip {
    height = 18;
    padding = 7;
    vertical-align = "center";
    border-radius = 4;
    background = $theme.input_background;
}

class info-chip-label {
    font-size = 11;
    font-color = $theme.text_dim;
}

// Sits on any button whose fill is light enough that white text on it is unreadable.
// Applied to the button's Label, not the button.
class dark-label {
    font-color = $theme.text_dark;
}

// The subtle-filled button used for chrome that isn't a dialog action: the menu bar,
// the transport controls. Reads as a surface you can click rather than as a call to
// action.
class menu-button {
    background = $theme.divider;
    border-radius = 6;
    height = 24;

    state[hovered] = {
        background = $theme.divider_hover;
    }

    state[pressed] = {
        background = $theme.divider_pressed;
    }
}

// The same look at list-row scale ("+ Add track", "+ New instrument"). Standalone
// rather than a modifier on menu-button - both set height, and two classes setting one
// property is exactly what the rule at the top forbids.
class menu-row {
    width = 100%;
    height = 36;
    font-size = 14;
    border-radius = 6;
    background = $theme.divider;

    state[hovered] = {
        background = $theme.divider_hover;
    }

    state[pressed] = {
        background = $theme.divider_pressed;
    }
}

// Small inline buttons that sit next to a section header (the inspector's +Add/-Remove).
class chip-button {
    font-size = 11;
    border-radius = 6;
    background = $theme.surface;

    state[hovered] = {
        background = $theme.surface_raised;
    }
}

// The label inside a menu-button. Carries the color too: there is no `component label`
// rule to fall back on - see the note in EditorInterface.snx.ss.
class menu-label {
    font-size = 14;
    font-color = $theme.text;
}

// ---------------------------------------------------------------- fields

// Every text and number field is the instrument builder's: 32 tall (Sundex's default), its
// text 15 px and centred, a 4 radius. Sundex draws the same shades by default; they are
// restated here so the palette above owns them. Tag rules rather than a class, because nothing
// but a field carries these tags - unlike `component label` they can't reach the grid views'
// pooled labels. A field that differs (the builder's name and filter) says so with a class or
// an id, which apply after these.
component text-input {
    font-size = 15;
    padding = 5;
    border-radius = 4;
    font-color = $theme.text;
    placeholder-color = $theme.text_muted;
    background = $theme.input_background;
}

component numeric-input {
    font-size = 15;
    padding = 5;
    border-radius = 4;
    font-color = $theme.text;
    background = $theme.input_background;
    step-spacing = 4;
    unit-font-size = 12;
    unit-color = $theme.text_dim;
}

// A number field's - and + buttons (NumericInput's own tag for them).
component numeric-step {
    font-size = 14;
    border-radius = 4;
    background = $theme.surface_raised;

    state[hovered] = {
        background = $theme.divider_hover;
    }
}

// A field's name beside it in a form row (the inspector, the export options, the builder).
class field-label {
    font-size = 13;
    font-color = $theme.text_dim;
}

// ---------------------------------------------------------------- label roles

// A section/dialog title.
class title-label {
    font-size = 15;
    font-color = $theme.header;
}

// A dialog's own heading where it shouldn't be tinted (the plain "Duplicate track").
class heading-label {
    font-size = 15;
}

// Ordinary body copy inside a dialog or panel.
class body-label {
    font-size = 14;
}

// A field's name, or any explanatory text that shouldn't compete with the value.
class muted-label {
    font-size = 13;
    font-color = $theme.text_muted;
}

// The smallest muted text: helper lines under a control, transport timecodes.
// A small section heading inside a panel - the sound picker's "Selected"/"Available",
// the faithful editor's "Sounds"/"Actions"/"Sequence". Smaller than title-label, which
// heads a whole dialog.
class sound-section-header {
    font-size = 13;
    font-color = $theme.header;
}

class caption-label {
    font-size = 12;
    font-color = $theme.text_muted;
}

// Text painted over a grid canvas (bar numbers, gutter values) is not an element any
// more - it is a LabelBatch slot, colored from the canvas's own `label-color` setting.

// ---------------------------------------------------------------- tile cells

// The caption over a cell's tile - the faithful palette's instruments, the import warnings'
// left-out events: a name, then a count when there is one.
class cell-caption {
    direction = "horizontal";
    vertical-align = "center";
    spacing = 4;
}

class cell-name {
    font-size = 11;
    font-color = $theme.text;
}

// A tile draws one of something, so this is the only thing saying there is more than one -
// muted, but never hidden.
class cell-count {
    font-size = 11;
    font-color = $theme.text_muted;
}

// ---------------------------------------------------------------- structure

// A 1px full-width horizontal rule.
class rule {
    width = 100%;
    height = 1;
    background = $theme.divider;
}

// A percent-width spacer that soaks up free space so the elements after it land flush
// against the right edge - this framework has no space-between align.
class spacer {
    width = 100%;
}

// ---------------------------------------------------------------- popup menus

// Sundex.Components.Panels.DropdownMenu's two pieces. Tag rules rather than classes
// because a menu is opened from code at a cursor position - there is no markup to hang
// a class on - and unlike `component button` these two tags belong to nothing else, so
// they cannot reach the pooled labels or the code-tinted toolbar buttons.
component dropdown {
    background = $theme.surface;
    border-radius = 6;
    padding = 4;
    spacing = 2;
}

component dropdown-item {
    // The base fill has to exist for the hover override to have something to replace.
    background = "#00000000";
    border-radius = 4;
    padding = 6;
    font-size = 14;

    state[hovered] = {
        background = $theme.divider_hover;
    }

    state[pressed] = {
        background = $theme.divider_pressed;
    }
}
