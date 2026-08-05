// The editor's shared color tokens (Tokyo Night derived).
//
// These are read into EditorPalette at startup, not applied to elements: they feed
// draw code that paints raw renderables (grid lines, clip blocks, selection fills)
// rather than [NamedSetting] properties, so no class/id selector can reach them.
// Everything that IS an element property is styled by the other sheets in this
// directory instead - see Controls.snx.ss.
//
// Retuning the editor's whole look starts here: the control sheets quote these same
// hexes literally, since the style DSL has no variables.
id palette {
    // Chrome: the column/header fill, and the two raised shades above it.
    panel = "#16161e";
    surface = "#292e42";
    surface-raised = "#353a54";
    input-background = "#262936";

    // Hairlines and their hover state (menu buttons, transport fills, rules).
    divider = "#33344a";
    divider-hover = "#3f4160";

    // The two accents everything color-codes against.
    accent = "#4c6bcc";
    accent-yellow = "#e0af68";

    // Text, dimmest first.
    text-muted = "#565f89";
    text-dim = "#a8b3db";
    header = "#7aa2f7";

    // Transport/selection markers.
    selection-highlight = "#9bc0ff";
    playhead = "#c0caf5";
    danger-accent = "#f7768e";

    // Row fill for the selected track in the track list.
    row-selected = "#414868";
    // The transport progress bar's unfilled track.
    progress-track = "#404060";
}
