// The shortcut sheet. Every key is drawn the way the playfield draws a sound: the key is
// the tile's face, the modifier sits in the top-left corner where a sound's pitch goes,
// and what it does is the caption underneath, where a sound's value goes. The tiles are
// Home's resting blue step - the Visualizer's own colour there - and a pressed key lights
// in the playhead colour, the way Home's playhead lights the steps it passes.
//
// ShortcutSheet fades the tree through ElementAlpha, which scales against the alphas set
// here.

// ---------------------------------------------------------------- placement

// Nothing loaded: centred in the window, under the greeting and above the bar.
class sheet {
    x = 50%;
    y = 50%;
    anchor-x = "center";
    anchor-y = "center";
    width = auto;
    height = auto;
    padding = 24;
    direction = "vertical";
    spacing = 28;
    background = "#0e0f1600";
}

// A cover playing: the same place, as a card on the bar's colour - opaque for the same
// reason the bar is, a cover's background can be anything. Appended by SetClass, so it wins.
class card {
    padding = 20;
    background = "#0e0f16";
}

// ---------------------------------------------------------------- header

id header {
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 8;
}

id headline {
    font-size = 20;
    font-color = "#e4e8f5";
}

id subline {
    font-size = 14;
    font-color = "#8b93b8";
}

id footnote {
    font-size = 13;
    font-color = "#565f89";
}

// ---------------------------------------------------------------- rows

id groups {
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 14;
}

class group {
    width = auto;
    height = auto;
    direction = "horizontal";
    spacing = 4;
}

// As tall as a tile, so the eyebrow lines up with the keys rather than their captions.
class group-slot {
    width = 112;
    height = 48;
    vertical-align = "center";
}

// Tracked by hand with spaces - the sheet has no letter-spacing.
class group-name {
    font-size = 13;
    font-color = "#565f89";
}

// Wider than the tile so a two-word caption fits under it.
class column {
    width = 80;
    height = auto;
    direction = "vertical";
    horizontal-align = "center";
    spacing = 5;
}

class caption {
    font-size = 12;
    font-color = "#8b93b8";
}

// ---------------------------------------------------------------- tiles

class tile {
    width = 48;
    height = 48;
    border-radius = 6;
    background = "#4c6bcc66";
}

class lit { background = "#c0caf5"; }

class tile-face-slot {
    width = 100%;
    height = 100%;
    horizontal-align = "center";
    vertical-align = "center";
}

class tile-corner {
    width = 100%;
    height = auto;
    padding = 3;
    horizontal-align = "start";
}

class face {
    font-size = 20;
    font-color = "#e4e8f5";
}

// A key with a name rather than a character - Space, PgUp.
class face-word { font-size = 13; }

class modifier {
    font-size = 9;
    font-color = "#c0caf5";
}

// Dark text on the lit tile.
class lit-text { font-color = "#0e0f16"; }
