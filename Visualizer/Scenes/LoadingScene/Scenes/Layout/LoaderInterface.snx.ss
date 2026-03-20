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
    background = "#7aa2f7";
    border-radius = 8;
    width = auto;
    height = 40;
    
    state[hovered] = {
        background = "#9bc0ff";
    }
   
    state[pressed] = {
        background = "#4c78a8";
    }
}