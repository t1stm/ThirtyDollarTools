#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in mat4 aModel; // SoundData->Model
layout (location = 5) in vec4 aRGBA; // SoundData->RGBA
layout (location = 6) in float aOffsetPercentage; // SoundData->OffsetPercentage

out vec2 fragmentCoords;
out vec4 RGBA;
out float offsetPercentage;
out float localU;

uniform mat4 u_VPMatrix;
uniform vec4 u_UV;

vec2 getFragCoordsBasedOnVertexID(int vertexID) {
    vec2 uv0 = u_UV.xy;
    vec2 uv1 = u_UV.zw;

    int normedID = vertexID % 4; // four coordinates

    if (normedID == 0) return vec2(uv0.x, uv1.y);
    if (normedID == 1) return vec2(uv1.x, uv1.y);
    if (normedID == 2) return vec2(uv1.x, uv0.y);
    return uv0;
}

void main() {
    vec4 finalCoords = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    gl_Position = finalCoords;
    RGBA = aRGBA;
    vec2 fragCoords = getFragCoordsBasedOnVertexID(gl_VertexID);
    fragmentCoords = fragCoords;
    float uvWidth = u_UV.z - u_UV.x;
    localU = uvWidth > 0.0 ? (fragCoords.x - u_UV.x) / uvWidth : 0.0;
    offsetPercentage = aOffsetPercentage;
}
