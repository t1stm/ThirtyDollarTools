// The top bar's own sheet: where things sit. How the buttons look - header-button and the
// menu button, with their hover and open states - is in Controls.snx.ss, because those
// states are applied at runtime from the root's sheet, which never sees this file's rules.
// The alias is declared here rather than inherited: aliases are file-local, so
// Controls.snx.ss's import of the theme does not bring `$theme` into this file.
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Theme.snx.ss" as theme;

id editor-header {
    width = 100%;
    height = 40;
    background = $theme.panel;
}

// Padding 8 around a 28 px button in a 40 px bar: the inner 24 is short of the button, and
// centring puts it 6 from the top either way, which is what the button wants.
id editor-header-bar {
    width = 100%;
    height = 40;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    spacing = 10;
}

id editor-title {
    font-size = 16;
    font-color = $theme.header;
}

class header-divider {
    width = 1;
    height = 22;
    background = $theme.divider;
}

// A button's slot: as tall as the button. Its width is set from code, to the button's.
class header-slot {
    height = 28;
}

id file-actions {
    direction = "horizontal";
    vertical-align = "center";
    spacing = 6;
}
