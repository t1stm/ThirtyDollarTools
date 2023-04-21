#version 330 core
layout(location = 0) out vec4 color;

uniform vec4 u_Color;
uniform sampler2D u_Texture;

in vec2 v_Texture_xy;

void main() {
    vec4 texture_color = texture(u_Texture, v_Texture_xy);
    color = texture_color;
}