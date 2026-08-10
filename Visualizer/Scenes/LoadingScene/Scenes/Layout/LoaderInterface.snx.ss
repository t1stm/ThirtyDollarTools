import "Scenes/Layout/LoaderGradients.snx.ss";

id main-holder {
    width = 100%;
    height = 100%;
    // background = "#1a1b26";
}

id background-holder {
    width = 100%;
    height = 100%;
}

id main-view {
    anchor-x = "center";
    anchor-y = "center";
    x = 50%;
    y = 50%;

    width = 600px;
    height = auto;
    
    padding = 24;
    spacing = 16;
    direction = "vertical";
    horizontal-align = "start";
    
    background = "#16161e80"; // 50% opacity
    border-radius = 16;
}

// The first-run update opt-in, in main-view's place. Same box as the loader panel, but
// opaque - it is a question, not a status readout. Colors are the editor's palette
// (Scenes/Styles/Theme.snx.ss over there); that sheet can't be imported across
// assemblies, so the values are literal here, as everything else in this file is.
id update-prompt {
    anchor-x = "center";
    anchor-y = "center";
    x = 50%;
    y = 50%;

    width = 480px;
    height = auto;

    padding = 24;
    spacing = 14;
    direction = "vertical";
    horizontal-align = "start";

    background = "#16161e";
    border-radius = 16;
}

id update-title {
    font-size = 24;
    font-color = "#7aa2f7";
    font-weight = "bold";
}

id update-message {
    font-size = 15;
}

// The trailing button row: horizontal and flush right, against the vertical default
// the `flex` rule at the bottom of this sheet sets.
id update-actions {
    width = 100%;
    height = 36;
    direction = "horizontal";
    horizontal-align = "end";
    vertical-align = "center";
    padding = 0;
    spacing = 10;
}

// Standalone rather than a base class plus a modifier: state overrides resolve to the
// first class that declares them, so a modifier's hover would lose to the base's.
class prompt-button {
    font-size = 14;
    height = 36;
    border-radius = 6;
    background = "#33344a";

    state[hovered] = {
        background = "#3f4160";
    }
}

class prompt-button-primary {
    font-size = 14;
    height = 36;
    border-radius = 6;
    background = "#4c6bcc";

    state[hovered] = {
        background = "#6b82c4";
    }
}

id loader-title {
    font-size = 32;
    font-color = "#7aa2f7";
    font-weight = "bold";
}

id loader-label {
    font-size = 14;
    font-color = "#565f89";
}

id loader-progress {
    width = 100%;
    height = 16;
    border-radius = 50%;
}

id start-button {
    width = 100%;
    height = 44;
}

component flex {
    horizontal-align = "center";
    vertical-align = "center";
    direction = "vertical";
    padding = 10;
    spacing = 10;
}

component label {
    font-size = 16;
    font-color = "#d6dadc";
}

component progress {
    background = "#2a2e3a";
    foreground = "#7aa2f7";
}

component button {
    background = "#6b82c4";
    border-radius = 8;
    width = auto;
    height = 40;
    
    state[hovered] = {
        background = "#8599d4";
    }
   
    state[pressed] = {
        background = "#4c78a8";
    }
}