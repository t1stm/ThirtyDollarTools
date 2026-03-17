import "Scenes/Layout/LoaderGradients.snx.ss";

id main-holder {
    width = 100%;
    height = 100%;
    background = "#1a1b26";
}

id background-holder {
    width = 100%;
    height = 100%;
}

id home-view {
    anchor-x = "center";
    anchor-y = "center";
    x = 50%;
    y = 50%;

    width = 600px;
    height = auto;
    
    padding = 32;
    spacing = 16;
    direction = "vertical";
    horizontal-align = "center";
    
    background = "#16161e80"; // 50% opacity
    border-radius = 24;
}

id spacer {
    width = 100%;
    height = 40;
}

id home-title {
    font-size = 48px;
    font-color = "#7aa2f7";
}

class menu-button {
    width = 100%;
    height = 50;
}

component label {
    font-size = 18;
    font-color = "#d6dadc";
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
