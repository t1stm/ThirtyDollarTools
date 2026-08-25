// this style sheet will define the default look of example elements.
// these comments should be ignored by the parser.

// rules: 
// all numbers are floating point values.
// all numbers must be followed by a unit.
// all strings are literal values.
// colors must be written in hex format and prefixed with "#".
// special keywords like !override, !gradient, !deg and !direction are prefixed with "!".

animation fade-in {
    timing-function = "ease-in-out";
    keyframes = !keyframes [
        0% = { opacity = 0 },
        100% = { opacity = 1; loop = "reset" }
    ];
}

animation fade-out {
    timing-function = "ease-out";
    duration = 1s;
    keyframes = !keyframes [
        { opacity = 100% },
        { opacity = 0%; loop = "invert" }
    ];
}

animation small-move {
    timing-function = "ease-in";
    duration = 1s;
    keyframes = !keyframes [
        { transform = vec2(0, 0) }, // 0%
        33% = { transform = vec2(10px, -10px) },
        { transform = vec2(0, 0) } // 100%
    ];
}

component button {
    padding = 5px;
    background = "#010101ff";
    font-color = "#ffffff";
    font-size = 14px;
    border-radius = 5px;
    
    state[pressed] = !override {
        background = "#020202ff";
        border-radius = 10px;
    };
    
    state[hovered] = !override {
        background = "#020202ff"
    };
}

component label {
    font-size = 12px;
    font-color = "#ffffff";
}

class best-flex {
    spacing = 10px;
    padding = 20px;
    direction = "horizontal";
    align = "center";
}

id button-1 {
    background = "#ff0000ff";
    border-radius = 5px;
}

class gradient-linear {
    background = !gradient {
        type = "linear";
        direction = 90deg; // also accepts !direction right
        stops = !stops [
            0% = "#ff0000",
            50% = "#ffff00",
            100% = "#00ff00"
        ];
    };
}

// stops can be omitted. when omitted, the gradient will be linear.
class gradient-radial {
    background = !gradient {
        type = "radial";
        size = 16px;
        direction = !direction outward;
        stops = !stops [
            "#ff0000",
            "#ffff00",
            "#00ffff"
        ];
    };
}
component dropdown {
    background = "#1f2229ff";
    border-radius = 6px;
    padding = 4px;
    spacing = 2px;
    // width = 180px; // omit to auto-size to the widest item
}

component dropdown-item {
    background = "#00000000";
    font-size = 14px;
    padding = 6px;
    border-radius = 4px;

    // a state block can only override a property the base block declares - hence the
    // transparent background above.
    state[hovered] = !override {
        background = "#2e3240ff"
    };
}
