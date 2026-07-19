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

id main-view {
    width = 100%;
    height = 100%;
    
    padding = 24;
    spacing = 16;
    direction = "vertical";
    
    background = "#16161e80";
}

id top-bar {
    width = 100%;
    height = auto;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 16;
    background = "#16161e40";
    border-radius = 8;
    padding = 12;
   
}

id title {
    font-size = 32;
    font-color = "#7aa2f7";
    font-weight = "bold";
}

id content-area {
    width = auto;
    height = 100%;
    direction = "horizontal";
    spacing = 5;
}

id taiko-lane-list {
    width = 800px;
    height = 100%;
    direction = "vertical";
    spacing = 8;
    background = "#16161e40";
    border-radius = 8;
}

id available-sounds {
    width = 300px;
    height = 100%;
    direction = "horizontal";
    spacing = 8;
    background = "#16161e40";
    border-radius = 8;
    padding = 12;
}

id footer {
    width = 100%;
    height = auto;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 16;
}

id footer-buttons {
    width = auto;
    height = auto;
    direction = "horizontal";
    spacing = 8;
}

class spacer {
    width = 100%;
    height = 1;
}

component label {
    font-size = 16;
    font-color = "#d6dadc";
}

component button {
    background = "#6b82c4";
    border-radius = 8;
    padding = 12;
    width = auto;
    height = 40;
    
    state[hovered] = {
        background = "#8599d4";
    }
   
    state[pressed] = {
        background = "#4c78a8";
    }
}

class sound-item {
    background = "#16161e80";
    border-radius = 4;
    padding = 8;
    width = 100%;
    
    state[hovered] = {
        background = "#1a1b26";
    }
}

class lane-config {
    background = "#16161e80";
    border-radius = 8;
    padding = 16;
    spacing = 12;
    width = 100%;
}
