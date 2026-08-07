// The editor's palette, as sheet variables. Nothing but `var` declarations - import it
// with an alias and reach a value as `$theme.name`:
//
//     import "Scenes/Styles/Theme.snx.ss" as theme;
//     class rule { background = $theme.divider; }
//
// Aliased on purpose: variables are the only scoped thing in the DSL, and an alias is
// file-local, so a sheet that imports another sheet does not silently inherit these
// names. Every sheet that wants them says so.
//
// This is the editor's whole palette. The canvas shades at the bottom are named by
// Scenes/Views/GridViews.snx.ss and nothing else.

// ---------------------------------------------------------------- chrome

// The window behind everything, then the column/header fill and the two shades above it.
var background = "#1a1b26";
var panel = "#16161e";
var surface = "#292e42";
var surface_raised = "#353a54";
var input_background = "#262936";

// Hairlines, and the same fill as a clickable surface: a menu button at rest, hovered
// and held.
var divider = "#33344a";
var divider_hover = "#3f4160";
var divider_pressed = "#2b2c3f";

// ---------------------------------------------------------------- accents

// The primary action's fill.
var accent = "#4c6bcc";
var accent_hover = "#6b82c4";

// The lighter blue: section titles, and the fill of a secondary action.
var header = "#7aa2f7";
var header_hover = "#93b4f9";

// Destructive actions.
var danger = "#f7768e";
var danger_hover = "#f78fa2";

// The yellow alternative in a two-option dialog.
var accent_yellow = "#e0af68";
var accent_yellow_hover = "#e8bf82";

// ---------------------------------------------------------------- text

var text = "#d6dadc";
var text_dim = "#a8b3db";
var text_muted = "#565f89";

// Text on a fill light enough that `text` is unreadable on it. Happens to equal `panel`
// today; it is a separate name so retuning the chrome does not recolor those labels.
var text_dark = "#16161e";

// ---------------------------------------------------------------- one-offs

// The transport progress bar's unfilled track - dimmer than a divider, and the only
// place that shade is used.
var progress_track = "#404060";

// ---------------------------------------------------------------- metrics

// The track column's width. Also the x of everything right of it - the grid area and
// the hint bar both start where it ends, in two different sheets.
var track_column_width = 260;

// ---------------------------------------------------------------- canvas
//
// From here down: the two grid views' own shades. Most of these never touch an element
// - they paint grid lines batched into one instanced call, or pooled blocks reassigned
// every layout - so they reach the draw code as [NamedSetting] properties on the view
// itself, set by the arrangement-canvas/note-canvas rules in GridViews.snx.ss.

// Markers over the two canvas views.
var selection_highlight = "#9bc0ff";
var playhead = "#c0caf5";

// Row fill for the selected track in the track list.
var row_selected = "#414868";

// Kept apart from the chrome above so retuning the grid doesn't disturb it. The canvas
// is darker than any chrome panel.
var grid_background = "#11121a";

// Note editor line work, faintest first: every step, then the row lines, the octave
// (every 12th) emphasis, and the zero row's band.
var step_line = "#1c1f2b";
var row_line = "#1a1c26";
var octave_line = "#33384f";
var zero_row = "#1a1c29";

// Segment strips alternate between these two so boundaries are visible without a
// separating line. Same pair as surface/surface_raised, named apart because they are
// the grid's, not the chrome's.
var strip_segment_a = "#292e42";
var strip_segment_b = "#353a54";

// The !cut row pinned under the grid.
var cut_row = "#353a54";

// The drag-select rectangle. Translucent (the trailing 40 is its alpha) so the notes
// and clips underneath stay readable while the marquee is over them.
var marquee_fill = "#4c6bcc40";

// Stable per-sound note colors; a sound's name picks its index, so reordering or
// recoloring entries recolors existing projects' notes - that's the intent, it's a
// palette, not an identity. Blue, purple, green, orange, rose, teal, olive, violet.
var sound_palette = [
    "#4c6bcc",
    "#9e5cb5",
    "#3d9975",
    "#c77840",
    "#b8526b",
    "#478fab",
    "#a89447",
    "#7a70c7"
];
