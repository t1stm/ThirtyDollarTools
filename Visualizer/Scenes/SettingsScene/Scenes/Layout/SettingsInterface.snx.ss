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

id settings-view {
    anchor-x = "center";
    anchor-y = "center";
    x = 50%;
    y = 50%;

    width = 700px;
    height = auto;

    padding = 32;
    spacing = 16;
    direction = "vertical";

    background = "#16161e80";
    border-radius = 24;
}

id settings-header {
    width = 100%;
    height = auto;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 16;
}

id settings-title {
    font-size = 32;
    font-color = "#7aa2f7";
}

id back-button {
    width = 100;
    height = 40;
}

id settings-divider {
    width = 100%;
    height = 1;
    background = "#414868";
}

id settings-list {
    width = 100%;
    height = auto;
    direction = "vertical";
    spacing = 4;
}

component label {
    font-size = 16;
    font-color = "#d6dadc";
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
