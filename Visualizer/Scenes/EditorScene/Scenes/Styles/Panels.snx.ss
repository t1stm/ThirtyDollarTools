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
    // One size for every row, letter or not, so the names all start at the same x - a
    // faithful row's blip carries an "F" and needs the room for it.
    width = 13;
    height = 13;
    border-radius = 7;
    // The blip doubles as the row's reorder handle; see TrackListPanel.HandlePress.
    cursor = "ResizeY";
}

// A faithful track says so in its blip - see EditorTrack.
class track-color-blip-faithful {
    horizontal-align = "center";
    vertical-align = "center";
}

// Dark on purpose: the blip is a palette fill, every one of them light enough that the
// chrome's own text color would vanish on it.
class track-blip-letter {
    font-size = 9;
    font-color = $theme.text_dark;
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

// ---------------------------------------------------------------- faithful editor

// The faithful editor's content area. Darker than any chrome panel (the same shade the
// two grid canvases use) so the section boxes on top of it read as separate cards
// instead of running into each other.
class faithful-body {
    direction = "vertical";
    width = 100%;
    height = 100%;
    padding = 14;
    spacing = 12;
    background = $theme.grid_background;
}

// One titled box - Sounds, Actions, Sequence. Carries the fill; the scroller inside it
// stays transparent so only the box's own corners are rounded.
class faithful-section {
    direction = "vertical";
    width = 100%;
    height = 100%;
    padding = 14;
    spacing = 10;
    border-radius = 8;
    background = $theme.background;
}

// The Actions box is as wide as its grid needs and no wider, so Sounds takes the rest.
class faithful-section-actions {
    width = 336;
}

// The sequence box takes what the palette band leaves. Stated rather than derived: the
// DSL has no calc(), so this is 100% minus the band's 40% minus the body's own spacing.
class faithful-section-sequence {
    height = 58%;
}

// The palette band: the Sounds and Actions boxes side by side, above the sequence.
class faithful-palette {
    width = 100%;
    height = 40%;
    spacing = 12;
}

// The scrollers inside the boxes. No fill of their own - see faithful-section.
class faithful-instruments {
    direction = "vertical";
    width = 100%;
    height = 100%;
    spacing = 6;
}

class faithful-actions {
    width = 100%;
    height = 100%;
}

class faithful-sequence {
    width = 100%;
    height = 100%;
}

// One instrument's row: its name, then its sounds. The resting fill is stated (rather
// than left transparent) so the hover reads as the same surface getting brighter, which
// is what tells you which row a click will hit.
class faithful-palette-row {
    direction = "horizontal";
    vertical-align = "center";
    width = 100%;
    padding = 6;
    spacing = 10;
    border-radius = 6;
    background = $theme.surface;

    state[hovered] = {
        background = $theme.surface_raised;
    }
}

// The Sequence box's own header row: its title, then the follow/tool toggles pushed right.
class faithful-sequence-bar {
    direction = "horizontal";
    vertical-align = "center";
    width = 100%;
    spacing = 8;
}

// "Follow scroll" - a label-only toggle, no fill of its own in either state; the color
// is the whole signal (see EditorInterface.SetFollowActive).
class faithful-follow-button {
    font-size = 12;
    padding = 4;
}

// Its label at rest, and once it is on: the color is the toggle's only state, so the
// active class overrides nothing but that.
class follow-label {
    font-color = $theme.text_muted;
}

class follow-label-active {
    font-color = $theme.header;
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
