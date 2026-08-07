// Styles used only by the sound picker grid, so they live next to SoundPicker.cs
// rather than in Scenes/Styles.
//
// Note what is NOT here: the icon cells themselves. Their size is computed per sound
// from the atlas aspect ratio (a wide sound gets a wide cell), so it can't come from a
// fixed value in a sheet - see SoundPicker.SoundIcon.
import "Scenes/Styles/Theme.snx.ss" as theme;

class sound-picker {
    direction = "vertical";
    spacing = 8;
    padding = 8;
}

// A wrapping row of sound icons.
class sound-grid {
    direction = "horizontal";
    width = 100%;
    wrap = true;
    spacing = 6;
}

// The "Selected" grid plus the scroll-adjust hint pinned to its right. Deliberately
// non-wrapping: the icon grid inside it wraps within whatever width the hint leaves.
class sound-selected-row {
    direction = "horizontal";
    width = 100%;
    spacing = 14;
}

// The "Selected" / "Available" section headings.
class sound-section-header {
    font-size = 13;
    font-color = $theme.header;
}

// The vertical hairline between the selected grid and the scroll-adjust hint. Height
// matches one icon cell.
class sound-keybind-divider {
    width = 1;
    height = 40;
    background = $theme.divider;
}

// The scroll-adjust hint text itself.
class sound-keybind-note {
    font-size = 12;
    font-color = $theme.text_muted;
}

// The value/volume/pan readout under a selected icon.
class sound-adjustment-label {
    font-size = 10;
}
