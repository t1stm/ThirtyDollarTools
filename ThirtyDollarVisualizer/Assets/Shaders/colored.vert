#version 330 core

layout (location = 0) in vec3 aPosition;

uniform mat4 u_Model;
uniform mat4 u_Projection;

void main() {
    vec4 final_coords = u_Projection * u_Model * vec4(aPosition, 1.0);
    gl_Position = final_coords;
}
