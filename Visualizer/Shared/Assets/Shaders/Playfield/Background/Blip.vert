#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in vec2 aUV; // Static Quad UV
layout (location = 2) in mat4 aModel; // BackgroundBlip->Model
layout (location = 6) in vec4 aRGBA; // BackgroundBlip->RGBA

out vec2 fragmentCoords;
out vec4 RGBA;

uniform mat4 u_VPMatrix;

void main() {
    vec4 finalCoords = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    gl_Position = finalCoords;
    fragmentCoords = aUV;
    RGBA = aRGBA;
}