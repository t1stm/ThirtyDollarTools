#version 330 core
layout(location = 0) in vec4 position;
layout(location = 1) in vec2 texture_xy;

out vec2 v_Texture_xy;

uniform mat4 u_MVP;

void main() {
    gl_Position = position * u_MVP;
    v_Texture_xy = texture_xy;
}