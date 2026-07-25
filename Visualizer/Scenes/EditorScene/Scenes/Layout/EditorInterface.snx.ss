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

// Subtle-filled menu-bar button (Load/Save/Export). state[] is safe here — real
// buttons aren't PropagateAlpha-managed the way the old loose labels were.
// pressed is overridden so it doesn't fall through to `component button`'s blue.
class menu-button {
    background = "#33344a";
    border-radius = 6;
    height = 24;

    state[hovered] = {
        background = "#3f4160";
    }

    state[pressed] = {
        background = "#2b2c3f";
    }
}

// The label inside a menu-button: 14px, else `component label` makes it 16.
class menu-label {
    font-size = 14;
}

class header-divider {
    width = 1;
    height = 24;
    background = "#33344a";
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
