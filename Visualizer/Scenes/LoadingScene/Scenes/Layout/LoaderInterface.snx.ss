id main-holder {
    width = 100%;
    height = 100%;
}

id main-view {
    x = 25%;
    y = 25%;
    width = 50%;
    height = 50%;
    background = "#0a0047";
    border-radius = 10;
}

component flex {
    direction = "vertical";
    padding = 10;
    spacing = 10;
}

component label {
    font-size = 16;
    color = "#ffffff";
}

component progress {
    width = 100%;
    height = 16px;
    border-radius = 10;
    background = !gradient {
        type = "linear";
       
        stops = [
            "#004687",
            "#ff4499"
        ]
    }
}

component button {
    background = "#00ffd2";
    border-radius = 10;
    width = auto;
    height = 40;
}