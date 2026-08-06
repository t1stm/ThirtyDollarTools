// variables are constants declared with "var" and used with "$".
// they are substituted when the file is parsed, so they must be declared before they're used.

// a plain import brings the imported file's variables into this file's scope.
import "Sundex.Style.DSL/Examples/theme.snx.ss";

// a named import keeps them behind an alias instead, reachable as "$alias.name".
// classes, ids and components are still merged globally either way.
import "Sundex.Style.DSL/Examples/theme.snx.ss" as theme;

// a variable holds any value the parser understands, not just colors.
var card_radius = 8px;
var card_shadow = {
    blur = 4px;
    color = "#00000088";
};

// declaring a variable that came from an import overrides it for the rest of this file.
var accent_color = "#00ff00ff";

component label {
    font-color = $text_color;
    font-size = 12px;
}

class card {
    background = $surface_color;
    padding = $padding_large;
    border-radius = $card_radius;
    shadow = $card_shadow;

    // the local declaration above wins here...
    border-color = $accent_color;

    // ...while the alias still sees theme.snx.ss's original value.
    state[hovered] = !override {
        border-color = $theme.accent_color;
    };
}
