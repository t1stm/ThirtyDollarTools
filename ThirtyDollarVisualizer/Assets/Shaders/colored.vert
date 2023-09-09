#version 330 core

layout (location = 0) in vec4 aPosition;

uniform vec2 u_ViewportSize;
uniform vec3 u_OffsetRelative;
uniform vec3 u_CameraPosition;

void main() {
    float pixel_x = aPosition.x + u_OffsetRelative.x + u_OffsetRelative.x + u_CameraPosition.x;
    float pixel_y = (u_ViewportSize.y - aPosition.y) + u_OffsetRelative.y + u_CameraPosition.y;

    float nx = pixel_x / u_ViewportSize.x * 2.0 - 1.0;
    float ny = pixel_y / u_ViewportSize.y * 2.0 - 1.0;
    
    gl_Position = vec4(nx, ny, aPosition.z, 1.0);
}
