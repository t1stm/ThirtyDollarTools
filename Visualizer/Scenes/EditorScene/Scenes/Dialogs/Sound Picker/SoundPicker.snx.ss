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
