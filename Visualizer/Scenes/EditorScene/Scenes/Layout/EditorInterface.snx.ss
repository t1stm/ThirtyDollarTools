// The editor's root sheet. It owns the window-level regions below and pulls in every
// other sheet: the shared vocabulary from Scenes/Styles, plus the component-local ones
// that sit next to the .cs file they belong to.
//
// Editing any of these changes the editor's look without a rebuild - the sheets are
// loaded at startup, and code-built components take their styling from here too
// (a panel hands its sheet to whatever is added to it), so nothing but genuinely
// per-frame values is compiled in.
import "Scenes/Styles/Palette.snx.ss";
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Dialogs.snx.ss";
import "Scenes/Styles/Panels.snx.ss";
import "Scenes/Layout/InspectorPanel.snx.ss";
import "Scenes/Dialogs/SoundPicker.snx.ss";
import "Scenes/Views/GridViews.snx.ss";

id main-holder {
    width = 100%;
    height = 100%;
    background = "#1a1b26";
}

// A slim menu-bar strip: identity labels + Load/Save/Export as plain
// clickable text (hover feedback is wired in EditorInterface.cs, not here -
// PropagateAlpha-managed elements must not use state[hovered]).
id editor-header {
    width = 100%;
    height = 32;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    spacing = 14;
    background = "#16161e";
}

id editor-title {
    font-size = 16;
    font-color = "#7aa2f7";
}

id project-name {
    font-size = 14;
    font-color = "#d6dadc";
}

id project-bpm {
    font-size = 14;
    font-color = "#565f89";
}

// menu-button / menu-label now live in Scenes/Styles/Controls.snx.ss - the menu bar
// was never the only place that wanted a subtle-filled button.

class header-divider {
    width = 1;
    height = 24;
    background = "#33344a";
}

// Track column and grid area heights/widths are set from
// EditorInterface.Resize - the window remainder isn't expressible here.
id track-column {
    y = 32;
    width = 260;
    background = "#16161e";
}

id grid-area {
    x = 260;
    y = 32;
    direction = "horizontal";
}

// X is set from EditorInterface.Resize (window remainder). Width must match
// InspectorPanel.PanelWidth.
id inspector-column {
    y = 32;
    width = 300;
    background = "#16161e";
}

// A slim status/hint strip under the grid area only - it sits between the track column
// and the inspector, which both run full height. A static gesture/shortcut legend by
// default, swapped for contextual text on hover (see EditorInterface.SetHint). Y and
// width are set from EditorInterface.Resize, same reason as grid-area's.
id hint-bar {
    x = 260;
    height = 26;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    background = "#16161e";
}

// Indents the hint text past the active grid's gutter so it starts at the first
// column instead of under the lane header. Width is set from
// EditorInterface.AlignHintToGrid - it differs per view.
id hint-gutter {
    height = 1;
}

id hint-label {
    font-size = 12;
    font-color = "#565f89";
}

// No `component label` / `component button` rules on purpose.
//
// A tag rule applies to EVERY element of that kind, and since code-built elements are
// styled now too, that means the hundreds of labels the grid views pool and every
// button whose fill is owned by code - the Draw/Select toggles tint themselves from
// EditorState.ActiveTool, and a `background` here would be re-created underneath them
// on the next hover, dropping the active tint. A `font-size` here would likewise
// overrule every label that set its own.
//
// So every look is a class or an id. What used to live here - the blue button fill and
// the 16px label default - was already dead: all three menu-bar buttons carry
// menu-button, and every label in this file sets its own size.
