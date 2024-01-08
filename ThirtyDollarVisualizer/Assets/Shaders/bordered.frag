#version 330 core

out vec4 color;

uniform vec4 u_Color;
uniform vec2 u_PositionPx;
uniform vec2 u_ScalePx;

uniform float u_BorderSizePx;
uniform vec4 u_BorderColor;

void main() {
    vec2 coords = gl_FragCoord.xy;
    
    bool check_start = coords.x < (u_PositionPx.x + u_BorderSizePx) || 
        coords.y < (u_PositionPx.y + u_BorderSizePx);
    
    bool check_end = coords.x > (u_PositionPx.x + u_ScalePx.x - u_BorderSizePx) || 
        coords.y > (u_PositionPx.y + u_ScalePx.y - u_BorderSizePx);
    
    bool is_border = check_start && check_end;
    
    color = is_border ? u_BorderColor : u_Color;
}
