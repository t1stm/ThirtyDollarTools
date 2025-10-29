#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in mat4 aModel; // SoundData->Model
layout (location = 5) in vec4 aRGBA; // SoundData->RGBA

out vec2 fragmentCoords;
out vec4 RGBA;

uniform mat4 u_VPMatrix;
uniform vec2 u_UV0;
uniform vec2 u_UV1;

vec2 getFragCoordsBasedOnVertexID(int vertexID) {
    int normedID = vertexID % 4; // four coordinates

    if (normedID == 0) return vec2(u_UV0.x, u_UV1.y);
    if (normedID == 1) return vec2(u_UV1.x, u_UV1.y);
    if (normedID == 2) return vec2(u_UV1.x, u_UV0.y);
    return u_UV0;
}

void main() {
    vec4 finalCoords = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    gl_Position = finalCoords;
    RGBA = aRGBA;
    fragmentCoords = getFragCoordsBasedOnVertexID(gl_VertexID);
}