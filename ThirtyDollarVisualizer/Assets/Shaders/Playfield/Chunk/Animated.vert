#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in mat4 aModel; // SoundData->Model
layout (location = 5) in vec4 aRGBA; // SoundData->RGBA

out vec2 fragmentCoords;
out vec4 RGBA;

uniform mat4 u_VPMatrix;
uniform vec2 u_UV0;
uniform vec2 u_UV1;
uniform vec2 u_UV2;
uniform vec2 u_UV3;

void setFragCoordsBasedOnVertexID() {
    int vertexID = gl_VertexID;
    int normedID = vertexID % 4; // four coordinates

    switch (normedID) {
        case 0:
            fragmentCoords = u_UV3;
            break;
        case 1:
            fragmentCoords = u_UV2;
            break;
        case 2:
            fragmentCoords = u_UV1;
            break;
        case 3:
            fragmentCoords = u_UV0;
            break;
    }
}

void main() {
    vec4 finalCoords = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    gl_Position = finalCoords;
    RGBA = aRGBA;
    setFragCoordsBasedOnVertexID();
}