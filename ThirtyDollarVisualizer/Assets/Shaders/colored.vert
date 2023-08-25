#version 330 core

layout (location = 0) in vec4 aPosition;

uniform vec2 u_ViewportSize;

void main() {
    float nx = aPosition.x / u_ViewportSize.x * 2f - 1f;
    float ny = aPosition.y / u_ViewportSize.y * 2f - 1f;
    
    gl_Position = vec4(nx, ny, 0, 1f);
}
