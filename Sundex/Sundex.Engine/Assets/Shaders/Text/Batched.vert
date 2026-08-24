#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec4 aUVRect;
layout (location = 2) in vec3 aTranslateXYZ;
layout (location = 3) in vec3 aScale;
layout (location = 4) in vec4 aColor;
layout (location = 5) in vec4 aClipRect;

uniform mat4 uVPMatrix;
out vec2 vFragTexCoord;
out vec4 vColor;
// UI-space position of this fragment, and the box it is confined to. The glyph's
// transform is applied before uVPMatrix, so this stage already holds the same
// absolute UI units ClipRect is written in - no viewport size or gl_FragCoord
// unflipping needed downstream. flat: one value per instance, never interpolated.
out vec2 vUIPosition;
flat out vec4 vClipRect;

vec2 getFragCoordsBasedOnVertexID(int vertexID) {
    int normedID = vertexID % 4; // four coordinates

    vec2 uv0 = aUVRect.xy;
    vec2 uv1 = aUVRect.zw;

    if (normedID == 0) return vec2(uv0.x, uv0.y);
    if (normedID == 1) return vec2(uv1.x, uv0.y);
    if (normedID == 2) return vec2(uv1.x, uv1.y);
    return vec2(uv0.x, uv1.y);
}

void main() {
    mat4 transformMatrix = mat4(
    aScale.x, 0.0, 0.0, 0.0,
    0.0, aScale.y, 0.0, 0.0,
    0.0, 0.0, 1.0, 0.0,
    aTranslateXYZ.x, aTranslateXYZ.y, aTranslateXYZ.z, 1.0
    );

    vec4 ui_coords = transformMatrix * vec4(aPosition.xyz, 1.0);
    vec4 final_coords = uVPMatrix * ui_coords;

    vUIPosition = ui_coords.xy;
    vClipRect = aClipRect;
    vColor = aColor;
    vFragTexCoord = getFragCoordsBasedOnVertexID(gl_VertexID);
    gl_Position = final_coords;
}