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

// The default dialog action: bland fill, for anything that isn't the primary choice.
class dialog-button {
    font-size = 14;
    border-radius = 6;
    background = $theme.divider;

    state[hovered] = {
        background = $theme.divider_hover;
    }
}

// The affirmative action of a dialog (Save, Export, Import as track).
class dialog-button-primary {
    font-size = 14;
    border-radius = 6;
    background = $theme.accent;

    state[hovered] = {
        background = $theme.accent_hover;
    }
}

// Destructive actions (Delete, Discard). Light fill, so its label is darkened
// separately - see the dark-label class below.
class dialog-button-danger {
    font-size = 14;
    border-radius = 6;
    background = $theme.danger;

    state[hovered] = {
        background = $theme.danger_hover;
    }
}

// The light-blue action (Duplicate, a row's Edit). Also needs a dark label.
class dialog-button-light {
    font-size = 14;
    border-radius = 6;
    background = $theme.header;

    state[hovered] = {
        background = $theme.header_hover;
    }
}

// The yellow alternative in a two-option dialog. Dark label as well.
class dialog-button-alt {
    font-size = 14;
    border-radius = 6;
    background = $theme.accent_yellow;

    state[hovered] = {
        background = $theme.accent_yellow_hover;
    }
}

// Height only, so it composes with any of the fills above: the import dialog's two
// options must line up across the divider between them.
class dialog-button-tall {
    height = 40;
}

// Shape without a fill, for a button that reads as a bare label - the track context
// menu's Cancel, which has never been filled like the other dialogs' are.
class dialog-button-shape {
    font-size = 14;
    border-radius = 6;
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

// The standard single-line text/number field.
class text-field {
    font-size = 14;
    border-radius = 4;
    background = $theme.input_background;
}

// Height only - composes with text-field where a row has a fixed height to fill.
class text-field-tall {
    height = 32;
}

// ---------------------------------------------------------------- label roles

// A section/dialog title.
class title-label {
    font-size = 15;
    font-color = $theme.header;
}

// The larger title used by the export dialog's heading.
class title-label-large {
    font-size = 18;
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
