#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in mat4 aModel; // StaticSound->SoundData->Model
layout (location = 5) in vec4 aRGBA; // StaticSound->SoundData->RGBA
layout (location = 6) in float aOffsetPercentage; // StaticSound->SoundData->OffsetPercentage
layout (location = 7) in vec2 aUV0;
layout (location = 8) in vec2 aUV1;

out vec2 fragmentCoords;
out vec4 RGBA;
out float offsetPercentage;
out float localU;

uniform mat4 u_VPMatrix;

vec2 getFragCoordsBasedOnVertexID(int vertexID) {
    int normedID = (2 + vertexID) % 4; // four coordinates

    if (normedID == 0) return vec2(aUV1.x, aUV0.y);
    if (normedID == 1) return vec2(aUV0.x, aUV0.y);
    if (normedID == 2) return vec2(aUV0.x, aUV1.y);
    return vec2(aUV1.x, aUV1.y);
}

void main() {
    vec4 finalCoords = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    gl_Position = finalCoords;
    RGBA = aRGBA;
    vec2 fragCoords = getFragCoordsBasedOnVertexID(gl_VertexID);
    fragmentCoords = fragCoords;
    float uvWidth = aUV1.x - aUV0.x;
    localU = uvWidth > 0.0 ? (fragCoords.x - aUV0.x) / uvWidth : 0.0;
    offsetPercentage = aOffsetPercentage;
}
