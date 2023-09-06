#version 330 core

layout (location = 0) in vec4 aPosition;
layout (location = 1) in vec2 aTextureCoordinates;

out vec2 fragment_coords;
out vec2 normalized_coords;

uniform vec2 u_ViewportSize;
uniform vec3 u_OffsetRelative;
uniform vec3 u_CameraPosition;

void main() {
    float pixel_x = aPosition.x + u_OffsetRelative.x + u_OffsetRelative.x + u_CameraPosition.x;
    float pixel_y = (u_ViewportSize.y - aPosition.y) + u_OffsetRelative.y + u_CameraPosition.y;
    
    float nx = pixel_x / u_ViewportSize.x * 2f - 1f;
    float ny = pixel_y / u_ViewportSize.y * 2f - 1f;
    
    gl_Position = vec4(nx, ny, aPosition.z, 1f);
    
    fragment_coords = aTextureCoordinates;
    normalized_coords = vec2(nx, ny);
}
