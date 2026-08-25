// The editor's persistent chrome: the track column and its list, the transport block,
// and the tool bars above each grid view. Modal forms live in Dialogs.snx.ss.
import "Scenes/Styles/Theme.snx.ss" as theme;

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

// One track-list row. Resting fill is transparent - a row only reads as a row when it is
// the selected one. Declared rather than omitted because removing track-row-selected only
// re-applies the sheet, so this is what a deselect restores; ColoredPlane skips a zero
// alpha instead of drawing it.
class track-row {
    direction = "horizontal";
    vertical-align = "center";
    width = 100%;
    height = 36;
    padding = 6;
    spacing = 10;
    border-radius = 6;
    background = "#00000000";
}

// The track's color, as a dot ahead of its name. Fully rounded, and sized only here -
// track-row's vertical-align centers it against the name and its spacing is the gap.
class track-color-blip {
    width = 10;
    height = 10;
    border-radius = 5;
    // The blip doubles as the row's reorder handle; see TrackListPanel.HandlePress.
    cursor = "ResizeY";
}

// Added by EditorTrack while the row is the selected track, removed when it isn't - so
// it must override track-row's fill, and be listed after it.
class track-row-selected {
    background = $theme.row_selected;
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

// The unfilled track and the played fill. Sheet-owned since the bar became markup;
// before that they were hand-built ColoredPlanes passed to the constructor.
id transport-progress {
    width = 100%;
    height = 8;
    border-radius = 4;
    background = $theme.progress_track;
    foreground = $theme.header;
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
    background = $theme.divider;

    state[hovered] = {
        background = $theme.divider_hover;
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

// A Draw/Select toggle at rest. The active highlight is one of the two classes below,
// added and removed by EditorInterface.AdoptToolButton as EditorState.ActiveTool
// changes - a runtime state, not a hover/press one, so it can't be a state[] block.
class tool-button {
    font-size = 12;
    border-radius = 6;
    background = $theme.surface;
}

// Draw's highlight: the primary accent, white label.
class tool-button-draw-active {
    background = $theme.accent;
}

// Select's: the yellow, light enough that its label needs darkening (dark-label goes
// on the button's Label alongside tool-label).
class tool-button-select-active {
    background = $theme.accent_yellow;
}

// The label inside a tool button - carries the color, since font-color isn't one of the
// settings a Button forwards to its Label.
class tool-label {
    font-color = $theme.text;
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
    background = $theme.input_background;
}

// ---------------------------------------------------------------- lane header

// The M/S gutter left of the arrangement.
id lane-header {
    background = $theme.panel;
}

// One M or S toggle.
class lane-toggle {
    width = 24;
    height = 24;
}

// Its label at rest. LaneHeader.RefreshChannels adds one of the two classes below when
// the lane is muted/soloed and removes it when it isn't, so each has to override this
// rule's color and be listed after it.
class lane-toggle-label {
    font-size = 12;
    font-color = $theme.text_muted;
}

class lane-toggle-muted {
    font-color = $theme.danger;
}

class lane-toggle-soloed {
    font-color = $theme.accent_yellow;
}
