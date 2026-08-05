// The editor's persistent chrome: the track column and its list, the transport block,
// and the tool bars above each grid view. Modal forms live in Dialogs.snx.ss.

// The vertical stack filling the track column: the scrollable list on top, the
// auto-sized transport block below it.
id track-column-body {
    direction = "vertical";
    width = 100%;
    height = 100%;
}

id track-list {
    width = 100%;
    height = 100%;
    spacing = 4;
}

// One track-list row. The fill is code-owned (it swaps to the selection color on
// click), so only the box is styled here.
class track-row {
    direction = "horizontal";
    vertical-align = "center";
    width = 100%;
    height = 36;
    padding = 6;
    spacing = 10;
    border-radius = 6;
}

// ---------------------------------------------------------------- transport

id transport {
    direction = "vertical";
    width = 100%;
    padding = 8;
    spacing = 8;
}

class transport-progress-row {
    width = 100%;
    spacing = 8;
    vertical-align = "center";
}

id transport-progress {
    width = 100%;
    height = 8;
    border-radius = 4;
}

class transport-buttons-row {
    width = 100%;
    spacing = 8;
}

// The transport's own buttons. Standalone rather than menu-button plus a width class:
// menu-button pins height to 24 and these size to their text.
class transport-button {
    font-size = 13;
    border-radius = 6;
    background = "#33344a";

    state[hovered] = {
        background = "#3f4160";
    }
}

// Play and Stop split the row; Back spans it.
class transport-half {
    width = 50%;
}

class transport-full {
    width = 100%;
}

// ---------------------------------------------------------------- grid tool bars

// The slim bar above each grid view holding the Draw/Select toggles.
class tool-bar {
    width = 100%;
    height = 40;
    spacing = 12;
    padding = 6;
}

// A Draw/Select toggle. Its fill is code-owned - the active highlight is a runtime
// toggle following EditorState.ActiveTool, not a hover/press state - so only the
// text size and corner are set here.
class tool-button {
    font-size = 12;
    border-radius = 6;
}

// The wrapper stacking a tool bar above its grid.
class grid-panel {
    direction = "vertical";
    width = 100%;
    height = 100%;
}

// The lane header + grid row inside the arrangement wrapper.
class grid-body {
    width = 100%;
    height = 100%;
}

class grid-view {
    width = 100%;
    height = 100%;
}

// The opened track's name field in the note editor's bar.
id opened-track-name {
    width = 220;
    font-size = 15;
    border-radius = 4;
    background = "#262936";
}

// ---------------------------------------------------------------- lane header

// The M/S gutter left of the arrangement.
id lane-header {
    background = "#16161e";
}

// One M or S toggle. Its label color is code-owned (it tracks mute/solo state), so
// only the box is styled here.
class lane-toggle {
    width = 24;
    height = 24;
}

class lane-toggle-label {
    font-size = 12;
}
