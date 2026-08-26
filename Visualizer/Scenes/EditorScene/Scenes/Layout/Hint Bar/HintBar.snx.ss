// A slim status/hint strip under the grid area only - it sits between the track column
// and the inspector, which both run full height. A static gesture/shortcut legend by
// default, swapped for contextual text on hover (see EditorInterface.SetHint). Y, width
// and height are set from EditorInterface - the window remainder isn't expressible here,
// and the height follows how many lines the current hint broke into.
import "Scenes/Styles/Theme.snx.ss" as theme;

id hint-bar {
    x = $theme.track_column_width;
    height = 26;
    direction = "horizontal";
    vertical-align = "center";
    padding = 8;
    background = $theme.panel;
}

// Indents the hint text past the active grid's gutter so it starts at the first
// column instead of under the lane header. Width is set from
// EditorInterface.AlignHintToGrid - it differs per view.
id hint-gutter {
    height = 1;
}

// The stack of hint lines. Auto-sized: it is as tall as the labels EditorInterface put
// in it, which is what the bar's own height is derived from.
id hint-lines {
    direction = "vertical";
    spacing = 2;
}

// One line of the hint. A class, not an id, because the lines after the first are added
// from code and have no markup to hang an id on.
class hint-line {
    font-size = 12;
    font-color = $theme.text_muted;
}
