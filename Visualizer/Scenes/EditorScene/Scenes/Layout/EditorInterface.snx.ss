id main-holder {
    width = 100%;
    height = 100%;
    background = "#1a1b26";
}

// A slim menu-bar strip: identity labels + Load/Save/Export as plain
// clickable text (hover feedback is wired in EditorInterface.cs, not here —
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

id load-button {
    font-size = 14;
}

id save-button {
    font-size = 14;
}

id export-button {
    font-size = 14;
}

class header-divider {
    width = 1;
    height = 24;
    background = "#33344a";
}

// Transport bar: Back, then play/pause/stop + progress + time.
id bottom-bar {
    width = 100%;
    height = 52;
    direction = "horizontal";
    vertical-align = "center";
    padding = 6;
    spacing = 14;
    background = "#16161e";
}

id back-button {
    width = 100;
}

id play-button {
    width = 80;
}

id stop-button {
    width = 80;
}

id transport-progress {
    width = 220;
    height = 8;
    border-radius = 4;
}

id transport-time {
    font-size = 14;
    font-color = "#565f89";
}

component progress {
    background = "#404060";
    foreground = "#7aa2f7";
}

// Track column and grid area heights/widths are set from
// EditorInterface.Resize — the window remainder isn't expressible here.
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

component label {
    font-size = 16;
    font-color = "#d6dadc";
}

component button {
    background = "#6b82c4";
    border-radius = 8;
    width = auto;
    height = 40;

    state[hovered] = {
        background = "#8599d4";
    }

    state[pressed] = {
        background = "#4c78a8";
    }
}
