// The editor's root sheet. It owns the window-level regions below and pulls in every
// other sheet: the shared vocabulary from Scenes/Styles, plus the component-local ones
// that sit next to the .cs file they belong to.
//
// Editing any of these changes the editor's look without a rebuild - the sheets are
// loaded at startup, and code-built components take their styling from here too (a
// panel hands its sheet to whatever is added to it). Even the grid views' line and
// block colors, which reach no element at all, are settings on the view itself now -
// see the canvas rules in Scenes/Views/GridViews.snx.ss.
import "Scenes/Styles/Theme.snx.ss" as theme;
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Dialogs.snx.ss";
import "Scenes/Styles/Panels.snx.ss";
import "Scenes/Layout/Inspector Panel/InspectorPanel.snx.ss";
import "Scenes/Dialogs/Sound Picker/SoundPicker.snx.ss";
import "Scenes/Views/GridViews.snx.ss";

id main-holder {
    width = 100%;
    height = 100%;
    background = $theme.background;
}

// editor-header's and hint-bar's rules moved out with their markup, into
// EditorHeader.snx.ss / HintBar.snx.ss. Nothing left here may name their ids: the root's
// cascade runs over the imported subtrees and would overwrite whatever the component
// sheet already set.

// Track column and grid area heights/widths are set from
// EditorInterface.Resize - the window remainder isn't expressible here.
id track-column {
    y = 32;
    width = $theme.track_column_width;
    background = $theme.panel;
}

id grid-area {
    x = $theme.track_column_width;
    y = 32;
    direction = "horizontal";
}

// X is set from EditorInterface.Resize (window remainder). Width must match
// InspectorPanel.PanelWidth.
id inspector-column {
    y = 32;
    width = 300;
    background = $theme.panel;
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
