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
