id main-holder {
    width = 100%;
    height = 100%;
}

class background-gradients {
    width = 100%;
    height = 100%;
}

id main-view {
    x = 50%;
    y = 50%;
    width = 50%;
    height = 50%;
    background = "#0a0047";
    border-radius = 10;
    
    // TODO new fields
    anchor-x = "center";
    anchor-y = "center";
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
    font-color = "#ffffff";
}

component progress {
    width = 100%;
    height = 16;
    border-radius = 10;
    background = !gradient {
        type = "linear";
        stops = [
            "#004687",
            "#ff4499"
        ]
    }
    
    foreground = "#00ffd2";
}

component button {
    background = "#00ffd2";
    border-radius = 10;
    width = auto;
    height = 40;
}