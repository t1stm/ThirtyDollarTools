// Styles used only by the right-side inspector (InspectorPanel + its row builder
// InspectorForm), so they live next to those files rather than in Scenes/Styles.
// Shared vocabulary the inspector also uses - text-field, muted-label, chip-button -
// comes from Scenes/Styles/Controls.snx.ss.
import "Scenes/Styles/Theme.snx.ss" as theme;

// The panel itself, built by InspectorShell.snx.xml. Its column width is
// EditorInterface.snx.ss's inspector-column; this fills it.
id inspector-panel {
    width = 100%;
    height = 100%;
}

// The scrollable row list filling the panel.
id inspector-rows {
    width = 100%;
    height = 100%;
    padding = 10;
    spacing = 8;
}

// The panel's vertical stack: rows, a rule, then the status strip pinned below.
id inspector-body {
    direction = "vertical";
    width = 100%;
    height = 100%;
}

// ---------------------------------------------------------------- status strip

id inspector-status {
    direction = "vertical";
    width = 100%;
    height = 40;
    padding = 8;
    spacing = 4;
}

// Sheet-owned since the strip became markup, same as transport-progress. The color path
// does rebuild the fill panel at 100% width, but ProgressBar.DoLayout re-derives it from
// Progress on every pass, so that width never survives to a frame.
// Hidden here rather than from logic so a standalone build of the shell starts in the
// same state the editor's does - the strip only appears while something is rendering,
// and SetStatus flips it back. `visible` is a [NamedSetting]; a visible= attribute on
// the node would have been dropped silently.
id inspector-status-bar {
    width = 100%;
    height = 6;
    visible = false;
    background = $theme.surface;
    foreground = $theme.accent;
}

// ---------------------------------------------------------------- rows

// A section header with action buttons trailing it on the same line.
class inspector-header-row {
    width = 100%;
    spacing = 8;
    vertical-align = "center";
}

// Boxes one automation/keyframe entry so adjacent entries read as distinct groups
// instead of one continuous run of rows. The shade comes from one of the two classes
// below - kept separate so they don't both set the same property as this one.
class inspector-card {
    direction = "vertical";
    width = 100%;
    padding = 8;
    spacing = 8;
    border-radius = 4;
}

// One shade above the panel background.
class inspector-card-entry {
    background = $theme.surface;
}

// One more shade up, for a keyframe nested inside an entry.
class inspector-card-keyframe {
    background = $theme.surface_raised;
}

// A checkbox row whose extra flags wrap onto a second line when the panel is narrow.
class inspector-check-row {
    width = 100%;
    spacing = 12;
    wrap = true;
    vertical-align = "center";
}

// The inspector's fields are narrower than a dialog's - the panel is only 300 wide.
class inspector-field {
    width = 140;
}

// The relative-change row's amount box, narrower again to leave room for its "×".
class inspector-field-narrow {
    width = 100;
    height = 32;
    font-size = 14;
}

// A checkbox's own label, and the read-only value shown by an info row.
class inspector-check-label {
    font-size = 13;
}

// The track color row: the chip and its Change button, centered against each other and
// filling the row's height so the pair centers against the row's label too.
class inspector-color-row {
    height = 34;
    spacing = 10;
    vertical-align = "center";
}

// The chip itself. Read-only - the swatch dialog does the picking - so it is a plain
// filled box. Its fill is set in code: it is the track's palette entry, not a look.
class inspector-color-chip {
    width = 36;
    height = 18;
    border-radius = 4;
}
