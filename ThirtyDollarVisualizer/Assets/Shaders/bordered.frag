#version 330 core

out vec4 color;

uniform vec4 u_Color;
uniform vec2 u_PositionPx;
uniform vec2 u_ScalePx;

uniform float u_BorderSizePx;
uniform vec4 u_BorderColor;

void main() {
    vec2 coords = gl_FragCoord.xy;
    
    bool insideBorder = all(
        greaterThanEqual(coords, u_PositionPx + u_BorderSizePx) &&
        lessThanEqual(coords, u_PositionPx + u_ScalePx - u_BorderSizePx));
    
    color = insideBorder ? u_BorderColor : u_Color;
}
