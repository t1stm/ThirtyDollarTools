// FileSelection's own rules, embedded in Sundex.Components and applied by the component itself
// (bug 2.9: they used to live only in the Visualizer's Dialogs.snx.ss and EditorDialogs.snx.ss,
// so any other host showed an empty list with the buttons outside the panel). The picker always
// uses this sheet, whatever its host's says.

class dialog-header {
    width = 100%;
    height = 28;
    spacing = 10;
    vertical-align = "center";
}

class dialog-title {
    font-size = 16;
    font-color = "#e9e6f2";
}

// 640 wide where there is room, never wider than what holds it.
class file-dialog {
    width = 640;
    max-width = 100%;
    height = 472;
    spacing = 12;
}

class file-location {
    width = 100%;
    height = 36;
    padding = 4;
    spacing = 2;
    vertical-align = "center";
    border-radius = 6;
    background = "#06060d";
}

class file-location-gap {
    width = 13;
    height = 18;
    horizontal-align = "center";
}

class file-location-rule {
    width = 1;
    height = 18;
    background = "#2a2638";
}

class file-crumbs {
    width = 100%;
    height = 28;
    spacing = 2;
    vertical-align = "center";
}

class file-crumb {
    height = 28;
    padding = 8;
    spacing = 6;
    vertical-align = "center";
    border-radius = 6;
    background = "#00000000";
}

class file-crumb-link {
    state[hovered] = {
        background = "#2a2638";
    }
}

class file-crumb-icon {
    font-size = 14;
    font-color = "#9b96ad";
}

class file-crumb-label {
    font-size = 13;
    font-color = "#9b96ad";
}

class file-crumb-label-current {
    font-color = "#e9e6f2";
}

class file-crumb-chevron {
    font-size = 12;
    font-color = "#696576";
}

class file-path-input {
    width = 100%;
    height = 28;
    padding = 6;
    font-size = 13;
    background = "#00000000";
}

class file-path-input-invalid {
    font-color = "#e85a4b";
}

class file-list {
    width = 100%;
    height = 100%;
    spacing = 2;
    border-radius = 6;
    background = "#06060d";
}

class file-row {
    width = 100%;
    height = 34;
    padding = 10;
    spacing = 10;
    vertical-align = "center";
    border-radius = 6;
}

class file-row-idle {
    background = "#00000000";

    state[hovered] = {
        background = "#28243a";
    }
}

class file-row-selected {
    background = "#3d394d";

    state[hovered] = {
        background = "#3d394d";
    }
}

class file-row-folder {
    font-size = 16;
    font-color = "#e9e6f2";
}

class file-row-icon {
    font-size = 16;
    font-color = "#9b96ad";
}

class file-row-name {
    font-size = 14;
    font-color = "#e9e6f2";
}

class file-row-date {
    font-size = 12;
    font-color = "#696576";
}

class file-list-empty {
    width = 100%;
    height = 100%;
    horizontal-align = "center";
    vertical-align = "center";
}

class file-list-empty-label {
    font-size = 14;
    font-color = "#9b96ad";
}

class file-list-note {
    width = 100%;
    height = 30;
    padding = 10;
    vertical-align = "center";
}

class file-list-note-label {
    font-size = 13;
    font-color = "#696576";
}

class file-overwrite {
    width = 100%;
    height = 18;
    spacing = 8;
    vertical-align = "center";
}

class file-overwrite-icon {
    font-size = 15;
    font-color = "#9b96ad";
}

class file-overwrite-label {
    font-size = 13;
    font-color = "#9b96ad";
}

class file-footer {
    width = 100%;
    height = 34;
    spacing = 8;
    vertical-align = "center";
}

class file-name-input {
    width = 240;
}

class file-ext-label {
    font-size = 13;
    font-color = "#696576";
}
