#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in mat4 aModel;
layout (location = 5) in vec4 aRGBA;

out vec4 vRGBA;

uniform mat4 u_VPMatrix;

void main() {
    gl_Position = u_VPMatrix * aModel * vec4(aPosition, 1.0);
    vRGBA = aRGBA;
}
