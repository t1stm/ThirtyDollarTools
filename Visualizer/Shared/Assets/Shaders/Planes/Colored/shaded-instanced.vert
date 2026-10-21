#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in mat4 aModel;
layout (location = 5) in vec4 aRGBA;
layout (location = 6) in vec4 aShape;

flat out vec4 vRGBA;
flat out vec4 vShape;
flat out vec2 vSize;
out vec2 vLocal;

uniform mat4 u_VPMatrix;

void main() {
    gl_Position = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    vRGBA = aRGBA;
    vShape = aShape;
    // The quad's size in px is the length of the model's first two axes - SetLine rotates
    // them, Set only scales, and a length reads both.
    vSize = vec2(length(aModel[0].xyz), length(aModel[1].xyz));
    vLocal = (aPosition.xy - 0.5) * vSize;
}
