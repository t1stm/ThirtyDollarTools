id bar-root {
    width = 100%;
    height = 60;
    y = 100%;
    anchor-y = "end";
    background = "#11111144"
}

id left-group {
    height = 100%;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    spacing = 8;
}

id center-group {
    x = 50%;
    anchor-x = "center";
    height = 100%;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    spacing = 8;
}

id right-group {
    x = 100%;
    anchor-x = "end";
    height = 100%;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    spacing = 8;
}

id progress-bar {
    width = 400;
    height = 8;
    border-radius = 4;
}

id current-time {
    font-size = 14;
    font-color = "#d6dadc";
}

id total-time {
    font-size = 14;
    font-color = "#d6dadc";
}

component progress {
    background = "#404060";
    foreground = "#7aa2f7";
    cursor = "pointer";
}

component button {
    background = "#6b82c466";
    border-radius = 8;
    width = 80;
    height = 36;
     
    state[hovered] = {
        background = "#9ab8ff";
    }
}

component label {
    font-size = 14;
    font-color = "#d6dadc";
}
