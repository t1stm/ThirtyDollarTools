#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
layout(location = 2) in mat4 iModel;
layout(location = 6) in vec4 iColor;
layout(location = 7) in uint iTexIndex;

uniform mat4 uViewProj;

out vec2 fragTexCoord;
out vec4 fragColor;
flat out uint fragTexIndex;

void main() {
    vec4 final_coords = uViewProj * iModel * vec4(aPosition, 1.0);
    gl_Position = final_coords;
    
    fragTexCoord = aTexCoord;
    fragColor = iColor;
    fragTexIndex = iTexIndex;
}