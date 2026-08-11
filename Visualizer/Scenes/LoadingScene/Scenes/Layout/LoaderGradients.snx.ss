// Drifting background gradients. Named for the loader because it is where they started,
// but the loader dropped them - the sound field is its background now, and the settings
// screen dropped them after it too. The one sheet that still imports them is in another
// assembly and reaches this file by path, so it stays here, at this path, and stays
// embedded in LoadingScene:
//   DrumMasterScene/UI/Select Sounds/SelectSounds.snx.ss

animation float-slow {
    timing-function = "ease-in-out";
    duration = 18s;
    keyframes = !keyframes [
        0% = { transform = vec2(0, 0) },
        25% = { transform = vec2(100px, 40px) },
        50% = { transform = vec2(40px, -120px) },
        75% = { transform = vec2(-80px, -30px) },
        100% = { transform = vec2(0, 0); loop = "reset" }
    ];
}

animation float-medium {
    timing-function = "ease-in-out";
    duration = 12s;
    keyframes = !keyframes [
        0% = { transform = vec2(0, 0) },
        33% = { transform = vec2(-120px, 60px) },
        66% = { transform = vec2(80px, 100px) },
        100% = { transform = vec2(0, 0); loop = "reset" }
    ];
}

animation float-fast {
    timing-function = "ease-in-out";
    duration = 8s;
    keyframes = !keyframes [
        0% = { transform = vec2(0, 0) },
        50% = { transform = vec2(150px, -150px) },
        100% = { transform = vec2(0, 0); loop = "reset" }
    ];
}

animation pulse-soft {
    timing-function = "ease-in-out";
    duration = 8s;
    keyframes = !keyframes [
        0% = { opacity = 0.6 },
        50% = { opacity = 0.9 },
        100% = { opacity = 0.6; loop = "reset" }
    ];
}

id grad-1 {
    animations = ["float-slow", "pulse-soft"];
    background = !gradient {
        type = "radial";
        direction = !direction outward;
        stops = !stops [
            0% = "#7aa2f733", // AccentBlue 20%
            30% = "#4c78a820", // BlueDark 12%
            100% = "#00000000"
        ];
    };
    x = 15%;
    y = 20%;
    width = 800px;
    height = 800px;
    anchor-x = "center";
    anchor-y = "center";
}

id grad-2 {
    animations = ["float-fast"];
    background = !gradient {
        type = "radial";
        direction = !direction outward;
        stops = !stops [
            0% = "#c792ea26", // AccentMagenta 15%
            25% = "#7aa2f71a", // AccentBlue 10%
            100% = "#00000000"
        ];
    };
    x = 75%;
    y = 30%;
    width = 600px;
    height = 600px;
    anchor-x = "center";
    anchor-y = "center";
}

id grad-3 {
    animations = ["float-medium", "pulse-soft"];
    background = !gradient {
        type = "radial";
        direction = !direction outward;
        stops = !stops [
            0% = "#2bbac926", // AccentTeal 15%
            40% = "#16161e1a", // BgSurface 10%
            100% = "#00000000"
        ];
    };
    x = 40%;
    y = 85%;
    width = 900px;
    height = 900px;
    anchor-x = "center";
    anchor-y = "center";
}

id grad-4 {
    animations = ["pulse-soft", "float-slow"];
    background = !gradient {
        type = "radial";
        direction = !direction outward;
        stops = !stops [
            0% = "#ff966c1a", // AccentOrange 10%
            30% = "#c792ea10", // AccentMagenta 6%
            100% = "#00000000"
        ];
    };
    x = 85%;
    y = 75%;
    width = 500px;
    height = 500px;
    anchor-x = "center";
    anchor-y = "center";
}

id grad-5 {
    animations = ["float-fast", "pulse-soft"];
    background = !gradient {
        type = "radial";
        direction = !direction outward;
        stops = !stops [
            0% = "#9ece6a1a", // AccentGreen 10%
            60% = "#2bbac90d", // AccentTeal 5%
            100% = "#00000000"
        ];
    };
    x = 60%;
    y = 10%;
    width = 700px;
    height = 700px;
    anchor-x = "center";
    anchor-y = "center";
}
