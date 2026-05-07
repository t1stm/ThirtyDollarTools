#version 330 core

in vec2 fragmentCoords;
in vec4 RGBA;
in float offsetPercentage;
in float localU;

uniform sampler2D u_Texture;

out vec4 color;

void main() {
    vec4 textureColor = texture(u_Texture, fragmentCoords);

    color = textureColor * RGBA;
    if (localU < offsetPercentage) {
        color.a = color.a * 0.35;
    }
}
