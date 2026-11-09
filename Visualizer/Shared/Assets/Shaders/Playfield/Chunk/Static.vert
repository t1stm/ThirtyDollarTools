#version 330 core

layout (location = 0) in vec3 aPosition; // Static Quad Coords that only have one Static VBO
layout (location = 1) in vec4 aBox; // StaticSound->SoundData->Box: top-left corner, then width and height
layout (location = 2) in vec4 aRGBA; // StaticSound->SoundData->RGBA
layout (location = 3) in float aOffsetPercentage; // StaticSound->SoundData->OffsetPercentage
layout (location = 4) in uint aUVIndex; // StaticSound->UVIndex

// StaticSoundAtlas's UV table: xy is the image's top-left UV, zw its bottom-right.
layout (std140) uniform UVs {
    vec4 u_UVs[1024]; // StaticSoundAtlas.MaxImages
};

out vec2 fragmentCoords;
out vec4 RGBA;
out float offsetPercentage;
out float localU;

uniform mat4 u_VPMatrix;

vec2 getFragCoordsBasedOnVertexID(int vertexID, vec2 uv0, vec2 uv1) {
    int normedID = (2 + vertexID) % 4; // four coordinates

    if (normedID == 0) return vec2(uv1.x, uv0.y);
    if (normedID == 1) return vec2(uv0.x, uv0.y);
    if (normedID == 2) return vec2(uv0.x, uv1.y);
    return vec2(uv1.x, uv1.y);
}

void main() {
    vec4 finalCoords = u_VPMatrix * vec4(aPosition.xy * aBox.zw + aBox.xy, 0.0, 1.0);
    gl_Position = finalCoords;
    RGBA = aRGBA;
    vec4 uv = u_UVs[aUVIndex];
    vec2 fragCoords = getFragCoordsBasedOnVertexID(gl_VertexID, uv.xy, uv.zw);
    fragmentCoords = fragCoords;
    float uvWidth = uv.z - uv.x;
    localU = uvWidth > 0.0 ? (fragCoords.x - uv.x) / uvWidth : 0.0;
    offsetPercentage = aOffsetPercentage;
}
