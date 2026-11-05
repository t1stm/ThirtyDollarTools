// The update dialog and the Markdown it shows. Ids carry the update- prefix because a tag, a class
// and an id share one lookup, and Home's sheet imports this one.
import "Scenes/Styles/Theme.snx.ss" as theme;
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Dialogs.snx.ss";

// Fits inside the 1080x720 minimum window. The height goes when there are no notes to scroll.
id update-dialog {
    width = 680;
}

class update-dialog-notes {
    height = 640;
}

// ---------------------------------------------------------------- header

// UpdateDialog.cs sets the width: as wide as the title, up to what the subtitle and the × leave.
class update-title-clip {
    height = 28;
    vertical-align = "center";
}

// ---------------------------------------------------------------- build card

class update-build-side {
    direction = "vertical";
    spacing = 3;
}

class update-build-side-end {
    horizontal-align = "end";
}

class update-build-caption {
    font-size = 12;
    font-color = $theme.text_subtle;
}

class update-build-tag {
    font-size = 14;
    font-color = $theme.text;
}

class update-build-tag-newest {
    font-color = $theme.header;
}

class update-build-gain {
    width = 100%;
    direction = "vertical";
    horizontal-align = "center";
    spacing = 6;
}

class update-tiles {
    direction = "horizontal";
    spacing = 6;
}

// The steps on Home's band: a dim one for your build, a lit one per release you would gain.
class update-tile {
    width = 12;
    height = 12;
    border-radius = 2;
    background = "#414868";
}

class update-tile-lit {
    background = "#c0caf5";
}

// ---------------------------------------------------------------- notes

class update-notes {
    width = 100%;
    height = 100%;
    spacing = 10;
}

class update-empty {
    width = 100%;
    height = 120;
    horizontal-align = "center";
    vertical-align = "center";
    border-radius = 8;
    background = $theme.background;
}

// ---------------------------------------------------------------- actions

class update-release-page {
    direction = "horizontal";
    spacing = 6;
    vertical-align = "center";
}

class update-release-icon {
    font-size = 14;
    font-color = $theme.text_dim;
}

// ---------------------------------------------------------------- Markdown

class md-h1 {
    font-size = 18;
    font-color = $theme.text;
}

class md-h2 {
    font-size = 15;
    font-color = $theme.header;
}

class md-h3 {
    font-size = 14;
    font-color = $theme.text_dim;
}

class md-p {
    font-size = 14;
    font-color = $theme.text;
}

class md-link {
    font-color = $theme.header;
}

// One px of fill above and below keeps the line height; a space either side of the text is the
// rest of its padding. The 13 px text then shares the 14 px text's baseline.
class md-code {
    padding = 1;
    border-radius = 3;
    background = $theme.input_background;
}

class md-code-text {
    font-size = 13;
    font-color = $theme.text;
}

class md-code-block {
    width = 100%;
    padding = 8;
    border-radius = 6;
    background = $theme.input_background;
}

class md-list {
    width = 100%;
    direction = "vertical";
    spacing = 6;
}

class md-li {
    width = 100%;
    direction = "horizontal";
    spacing = 8;
}

class md-li-marker {
    font-size = 14;
    font-color = $theme.text_dim;
}

class md-quote {
    width = 100%;
    direction = "horizontal";
    spacing = 8;
}

class md-quote-gap {
    width = 6;
    height = 1;
}

class md-quote-text {
    font-size = 14;
    font-color = $theme.text_dim;
}

class md-image-frame {
    border-radius = 6;
    background = $theme.surface;
}

class md-image-icon {
    font-size = 28;
    font-color = $theme.text_subtle;
}

class md-image {
    border-radius = 6;

    // Room for the placeholder and no more: a dead image shouldn't hold a screenful of nothing.
    state[errored] = {
        height = 96;
    }
}
