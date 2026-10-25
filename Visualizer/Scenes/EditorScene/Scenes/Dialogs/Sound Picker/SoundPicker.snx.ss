// Styles used only by the sound picker grid, so they live next to SoundPicker.cs
// rather than in Scenes/Styles.
//
// Note what is NOT here: the icon cells themselves. They are fixed squares, with each
// image fitted inside at its atlas aspect ratio, so their size lives in code - see
// SoundPicker.SoundElementSize.
import "Scenes/Styles/Theme.snx.ss" as theme;

class sound-picker {
    direction = "vertical";
    spacing = 8;
    padding = 8;
}

// A wrapping row of sound icons. The cells are all one size, so every full row holds the
// same number of them.
class sound-grid {
    direction = "horizontal";
    width = 100%;
    wrap = true;
    spacing = 6;
}

// One icon cell. Declares the fill and corners sound-icon-picked changes, so unpicking puts
// them back.
class sound-icon {
    border-radius = 6;
    background = "#00000000";
}

// A pick that stays where it is in the grid (SoundPicker.MarkInPlace): a header-blue veil,
// and a check on the corner (sound-icon-check).
class sound-icon-picked {
    background = "#7aa2f733";
}

class sound-icon-check {
    width = 14;
    height = 14;
    horizontal-align = "center";
    vertical-align = "center";
    border-radius = 7;
    background = $theme.header;
}

class sound-icon-check-glyph {
    font-size = 10;
    font-color = $theme.text_dark;
}

// What the grid shows when the filter matches nothing: room for the icon, the line and the
// Clear filter button.
class sound-picker-no-match {
    height = 180;
}
