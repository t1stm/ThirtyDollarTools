#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in mat4 aModel; // StaticSound->SoundData->Model
layout (location = 5) in vec4 aRGBA; // StaticSound->SoundData->RGBA
layout (location = 6) in vec2 aUV0;
layout (location = 7) in vec2 aUV1;
layout (location = 8) in vec2 aUV2;
layout (location = 9) in vec2 aUV3;

out vec2 fragmentCoords;
out vec4 RGBA;

uniform mat4 u_VPMatrix;

void setFragCoordsBasedOnVertexID() {
    int vertexID = gl_VertexID;
    int normedID = vertexID % 4; // four coordinates

    switch (normedID) {
        case 0:
            fragmentCoords = aUV3;
            break;
        case 1:
            fragmentCoords = aUV2;
            break;
        case 2:
            fragmentCoords = aUV1;
            break;
        case 3:
            fragmentCoords = aUV0;
            break;
    }
}

void main() {
    vec4 finalCoords = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    gl_Position = finalCoords;
    RGBA = aRGBA;
    setFragCoordsBasedOnVertexID();
}