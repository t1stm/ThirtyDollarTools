// Colors for the two canvas views - the arrangement and the note editor - kept next
// to ArrangementView.cs/TrackEditorView.cs because nothing else uses them.
//
// Like Scenes/Styles/Palette.snx.ss these are read into EditorPalette rather than
// matched against elements: the grid, its lines and its blocks are painted from a
// LineBatch and a pool of reassigned planes, not from styled components, so there is
// no element for a selector to hit. The two views' shared shades are here rather than
// in Palette.snx.ss because retuning the grid shouldn't disturb the chrome.
//
// Named grid-palette, not grid-view: `class grid-view` in Panels.snx.ss sizes the two
// view elements, and an id of the same name would win over it on every shared property
// (ids beat classes in GetStyleValueForTag).
id grid-palette {
    // The canvas behind both grids - darker than any chrome panel.
    background = "#11121a";

    // Note editor line work, faintest first: every step, then beats, then the row
    // lines, the octave (every 12th) emphasis, and the zero row's band.
    step-line = "#1c1f2b";
    row-line = "#1a1c26";
    octave-line = "#33384f";
    zero-row = "#1a1c29";

    // Segment strips alternate between these two so boundaries are visible without
    // a separating line.
    strip-segment-a = "#292e42";
    strip-segment-b = "#353a54";

    // The !cut row pinned under the grid.
    cut-row = "#353a54";

    // The drag-select rectangle. Translucent (the trailing 40 is its alpha) so the
    // notes and clips underneath stay readable while the marquee is over them.
    marquee-fill = "#4c6bcc40";
}

// One track's clip on an arrangement lane. Its fill is code-owned - the view tints it
// per selection state every layout - so only the box and its label are here.
class clip-block {
    padding = 6;
}

class clip-label {
    font-size = 13;
}

// Stable per-sound note colors. A sound's index into this array is derived from its
// name, so reordering or recoloring entries recolors existing projects' notes -
// that's the intent, it's a palette, not an identity.
id sound-palette {
    colors = [
        "#4c6bcc", // blue
        "#9e5cb5", // purple
        "#3d9975", // green
        "#c77840", // orange
        "#b8526b", // rose
        "#478fab", // teal
        "#a89447", // olive
        "#7a70c7"  // violet
    ];
}
