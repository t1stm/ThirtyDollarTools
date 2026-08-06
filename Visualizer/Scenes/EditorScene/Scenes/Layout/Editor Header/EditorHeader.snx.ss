// The menu bar's own sheet. menu-button/menu-label come from the shared vocabulary -
// the strip is not the only place that wants a subtle-filled button.
import "Scenes/Styles/Controls.snx.ss";

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

class header-divider {
    width = 1;
    height = 24;
    background = "#33344a";
}
