// A slim status/hint strip under the grid area only - it sits between the track column
// and the inspector, which both run full height. A static gesture/shortcut legend by
// default, swapped for contextual text on hover (see EditorInterface.SetHint). Y and
// width are set from EditorInterface.Resize - the window remainder isn't expressible here.
id hint-bar {
    x = 260;
    height = 26;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    background = "#16161e";
}

// Indents the hint text past the active grid's gutter so it starts at the first
// column instead of under the lane header. Width is set from
// EditorInterface.AlignHintToGrid - it differs per view.
id hint-gutter {
    height = 1;
}

id hint-label {
    font-size = 12;
    font-color = "#565f89";
}
