#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec4 aUVRect;
layout (location = 2) in vec3 aTranslateXYZ;
layout (location = 3) in vec2 aScale;

uniform mat4 uVPMatrix;
out vec2 fragTexCoord;

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

    vec4 final_coords = uVPMatrix * transformMatrix * vec4(aPosition.xyz, 1.0);

    fragTexCoord = getFragCoordsBasedOnVertexID(gl_VertexID);
    gl_Position = final_coords;
}