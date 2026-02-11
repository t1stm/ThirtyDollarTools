#version 330 core

in vec2 fragmentCoords;
in vec4 RGBA;

uniform sampler2D u_Texture;

out vec4 color;

void main() {
    vec4 textureColor = texture(u_Texture, fragmentCoords);
    float textureAlpha = textureColor.a;
    
    color = textureColor * RGBA;
}