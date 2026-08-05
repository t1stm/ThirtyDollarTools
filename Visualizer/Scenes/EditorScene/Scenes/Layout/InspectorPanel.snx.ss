// Styles used only by the right-side inspector (InspectorPanel + its row builder
// InspectorForm), so they live next to those files rather than in Scenes/Styles.
// Shared vocabulary the inspector also uses - text-field, muted-label, chip-button -
// comes from Scenes/Styles/Controls.snx.ss.

// The panel itself. Its column width is EditorInterface.snx.ss's inspector-column;
// this fills it.
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

// Box only. A ProgressBar's `background`/`foreground` take whole Panels, and the
// sheet's color path rebuilds both at 100% width - which is wrong for the fill, whose
// width IS the progress. Its two planes stay code-built from the palette.
id inspector-status-bar {
    width = 100%;
    height = 6;
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
    background = "#292e42";
}

// One more shade up, for a keyframe nested inside an entry.
class inspector-card-keyframe {
    background = "#353a54";
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
