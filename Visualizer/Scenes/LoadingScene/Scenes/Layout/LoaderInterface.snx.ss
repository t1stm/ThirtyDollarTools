// The loader's own screen, and deliberately not the home screen's. Home is a centred
// poster; this is a full-bleed stage with a status strip along its bottom edge.
//
// DollarStoreLoaderBackground renders the sound sprites first and this sheet puts the
// strip on top of them, so the screen visibly fills with the files it is downloading.
// That is the whole idea of the screen, and it is why there is no card and no wordmark
// competing with it.
//
// The one thing over the sound field is the atmosphere: the same drifting gradients the
// settings and sound-picker screens use. The sound field is empty until the download
// starts, which on a first run is the entire setup - three panes of text over bare black.
// The gradients give that stretch something to sit on, and they are faint enough (10-20%
// alpha) to stay atmosphere once the sprites arrive underneath them.

import "Scenes/Layout/LoaderGradients.snx.ss";

// ---------------------------------------------------------------- palette

var surface    = "#0f1119f2";  // the strip: 95%, so the sound field stays faintly behind it
var rule       = "#242839";    // hairlines, and an unfilled segment
var text       = "#e4e8f5";    // headings and the current status line
var muted      = "#6b7396";    // eyebrows, body copy, the counter
var signal     = "#c0caf5";    // "this has happened" - the home screen's lit-step colour
var signal_dim = "#3d4468";    // the one segment currently being filled
var action     = "#4c6bcc";    // used exactly once per view, on the primary button
var action_hi  = "#6280dd";
var quiet      = "#191c2a";
var quiet_hi   = "#23273a";

// ---------------------------------------------------------------- stage and strip

// No background: the sound field is the background.
id stage {
    width = 100%;
    height = 100%;
}

// Holds the gradients from LoaderGradients.snx.ss and nothing else. Declared before the
// strip in the markup so the strip paints over it - siblings paint in declaration order.
id atmosphere {
    width = 100%;
    height = 100%;
}

// Anchored to the bottom edge and grown from zero height by LoaderInterface - the strip
// rises into view on boot, stands tall while the setup is up, and settles into a status
// bar when loading starts. Everything inside is placed from its top edge, so it all rises
// and settles with it.
id strip {
    x = 0;
    y = 100%;
    anchor-y = "end";
    width = 100%;
    height = 0;
    background = $surface;
}

id strip-rule {
    width = 100%;
    height = 1;
    background = $rule;
}

// ---------------------------------------------------------------- the meter
//
// One device for the whole screen: a row of segments that counts whatever is currently
// being counted - three while the setup asks its questions, thirty while the sounds come
// down. Thirty because that is what this program is called.

id ticks {
    x = 4%;
    y = 32;
    width = 92%;
    height = 6;
    direction = "horizontal";
    spacing = 4;
}

// Declares the resting background so removing tick-lit restores it.
class tick {
    height = 6;
    border-radius = 2;
    background = $rule;
}

class tick-lit { background = $signal; }
class tick-next { background = $signal_dim; }

// ---------------------------------------------------------------- status readout

id status {
    x = 4%;
    y = 58;
    width = 92%;
    height = 20;
}

id status-left {
    direction = "horizontal";
    vertical-align = "center";
    spacing = 12;
    width = auto;
    height = 20;
}

id status-message { font-color = $text; }
id status-detail { font-color = $muted; }

id status-percent {
    x = 100%;
    anchor-x = "end";
    font-color = $muted;
}

// ---------------------------------------------------------------- first-run setup
//
// Three panes in one slot. They overlap rather than flow, so LoaderInterface can slide and
// cross-fade between them without the strip reflowing underneath. X is a plain pixel
// offset the slide writes; the page margin lives on the two blocks inside instead.

class pane {
    x = 0;
    y = 84;
    width = 100%;
    height = 236;
}

class pane-text {
    x = 4%;
    y = 0;
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 20;
}

class pane-head {
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 8;
}

class pane-body {
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 6;
}

class pane-options {
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 10;
}

// Pinned bottom-right, in the same place on all three panes: the action does not move
// while you step through the setup.
class pane-actions {
    x = 96%;
    anchor-x = "end";
    y = 100%;
    anchor-y = "end";
    width = auto;
    height = 40;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 12;
}

// Tracked by hand - this sheet has no letter-spacing, and the spaces are what separate a
// section name from a sentence. Same device as the home screen's eyebrow.
class eyebrow {
    font-size = 12;
    font-color = $muted;
}

class pane-title {
    font-size = 30;
    font-color = $text;
}

class pane-line {
    font-size = 15;
    font-color = $muted;
}

// ---------------------------------------------------------------- controls
//
// No state[hovered] blocks on either button class. LoaderInterface owns their alpha while
// a pane cross-fades, and a state override would restore the base alpha mid-fade; the
// hover colour is set from OnHoverEnter/OnHoverExit instead, RGB only.

class button-primary { background = $action; }
class button-quiet { background = $quiet; }

component button {
    padding = 18;
    height = 40;
    border-radius = 8;
    font-size = 14;
}

component flex {
    direction = "vertical";
    horizontal-align = "start";
    vertical-align = "start";
    padding = 0;
    spacing = 0;
}

component label {
    font-size = 15;
    font-color = $text;
}
