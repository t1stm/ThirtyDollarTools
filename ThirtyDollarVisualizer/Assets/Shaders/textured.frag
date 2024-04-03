#version 330 core

in vec2 fragment_coords;

uniform sampler2D u_Texture;
uniform float u_DeltaAlpha;

out vec4 color;

void main() {
    vec4 texture_color = texture(u_Texture, fragment_coords);

    float texture_alpha = texture_color.a;
    float delta_alpha = u_DeltaAlpha;

    vec4 mixed_color;

    if (texture_alpha < 0.01) {
        mixed_color = texture_color;
    }
    else {
        mixed_color = vec4(texture_color.rgb, texture_alpha - delta_alpha);
    }

    color = mixed_color;
}