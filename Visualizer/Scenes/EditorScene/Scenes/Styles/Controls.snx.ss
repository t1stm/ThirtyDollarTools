// The editor's shared control vocabulary: buttons, text fields and label roles that
// more than one screen uses. Anything used by exactly one component lives in that
// component's own sheet next to its .cs instead.
//
// Code-built elements pick these up the same way markup ones do - a panel passes its
// sheet to whatever is added to it (Sundex.Components.Panels.Panel.AddChild), so
// setting Classes on an element is all it takes.
//
// ONE RULE when combining classes on an element: they must not set the same property.
// UIElement.Classes is a HashSet, so the order they are applied in is not defined, and
// two classes both setting `height` would give whichever won last. Where a variant has
// to override a shared value, put the override on the element's id instead - ids are
// always applied after classes (see UIElement.SetNamedSetting).

// ---------------------------------------------------------------- buttons

// The default dialog action: bland fill, for anything that isn't the primary choice.
class dialog-button {
    font-size = 14;
    border-radius = 6;
    background = "#33344a";

    state[hovered] = {
        background = "#3f4160";
    }
}

// The affirmative action of a dialog (Save, Export, Import as track).
class dialog-button-primary {
    font-size = 14;
    border-radius = 6;
    background = "#4c6bcc";

    state[hovered] = {
        background = "#6b82c4";
    }
}

// Destructive actions (Delete, Discard). Light fill, so its label is darkened
// separately - see the dark-label class below.
class dialog-button-danger {
    font-size = 14;
    border-radius = 6;
    background = "#f7768e";

    state[hovered] = {
        background = "#f78fa2";
    }
}

// The light-blue action (Duplicate, a row's Edit). Also needs a dark label.
class dialog-button-light {
    font-size = 14;
    border-radius = 6;
    background = "#7aa2f7";

    state[hovered] = {
        background = "#93b4f9";
    }
}

// The yellow alternative in a two-option dialog. Dark label as well.
class dialog-button-alt {
    font-size = 14;
    border-radius = 6;
    background = "#e0af68";

    state[hovered] = {
        background = "#e8bf82";
    }
}

// Height only, so it composes with any of the fills above: the import dialog's two
// options must line up across the divider between them.
class dialog-button-tall {
    height = 40;
}

// Shape without a fill, for the one button whose color is a constructor argument
// (ConfirmDialog's confirm action is red by default but the import flow tints it).
// Setting `background` here would replace the caller's plane.
class dialog-button-shape {
    font-size = 14;
    border-radius = 6;
}

// Sits on any button whose fill is light enough that white text on it is unreadable.
// Applied to the button's Label, not the button.
class dark-label {
    font-color = "#16161e";
}

// The subtle-filled button used for chrome that isn't a dialog action: the menu bar,
// the transport controls. Reads as a surface you can click rather than as a call to
// action.
class menu-button {
    background = "#33344a";
    border-radius = 6;
    height = 24;

    state[hovered] = {
        background = "#3f4160";
    }

    state[pressed] = {
        background = "#2b2c3f";
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
    background = "#33344a";

    state[hovered] = {
        background = "#3f4160";
    }

    state[pressed] = {
        background = "#2b2c3f";
    }
}

// Small inline buttons that sit next to a section header (the inspector's +Add/-Remove).
class chip-button {
    font-size = 11;
    border-radius = 6;
    background = "#292e42";

    state[hovered] = {
        background = "#353a54";
    }
}

// The label inside a menu-button. Carries the color too: there is no `component label`
// rule to fall back on - see the note in EditorInterface.snx.ss.
class menu-label {
    font-size = 14;
    font-color = "#d6dadc";
}

// ---------------------------------------------------------------- fields

// The standard single-line text/number field.
class text-field {
    font-size = 14;
    border-radius = 4;
    background = "#262936";
}

// Height only - composes with text-field where a row has a fixed height to fill.
class text-field-tall {
    height = 32;
}

// ---------------------------------------------------------------- label roles

// A section/dialog title.
class title-label {
    font-size = 15;
    font-color = "#7aa2f7";
}

// The larger title used by the export dialog's heading.
class title-label-large {
    font-size = 18;
    font-color = "#7aa2f7";
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
    font-color = "#565f89";
}

// The smallest muted text: helper lines under a control, transport timecodes.
class caption-label {
    font-size = 12;
    font-color = "#565f89";
}

// Text painted over a grid canvas (bar numbers, gutter values).
class grid-label {
    font-size = 11;
    font-color = "#a8b3db";
}

// ---------------------------------------------------------------- structure

// A 1px full-width horizontal rule.
class rule {
    width = 100%;
    height = 1;
    background = "#33344a";
}

// A percent-width spacer that soaks up free space so the elements after it land flush
// against the right edge - this framework has no space-between align.
class spacer {
    width = 100%;
}
