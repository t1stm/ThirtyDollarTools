// The menu bar's own sheet. menu-button/menu-label come from the shared vocabulary -
// the strip is not the only place that wants a subtle-filled button. The alias is
// declared here rather than inherited: aliases are file-local, so Controls.snx.ss's
// import of the theme does not bring `$theme` into this file.
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Theme.snx.ss" as theme;

// A slim menu-bar strip: identity labels + Load/Save/Export as plain
// clickable text (hover feedback is wired in Controls.snx.ss's menu-button, not with a
// state[hovered] block - PropagateAlpha-managed elements must not use those).
id editor-header {
    width = 100%;
    height = 32;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    spacing = 14;
    background = $theme.panel;
}

id editor-title {
    font-size = 16;
    font-color = $theme.header;
}

id project-name {
    font-size = 14;
    font-color = $theme.text;
}

id project-bpm {
    font-size = 14;
    font-color = $theme.text_muted;
}

class header-divider {
    width = 1;
    height = 24;
    background = $theme.divider;
}
