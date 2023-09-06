#version 330 core

in vec2 fragment_coords;
in vec2 normalized_coords;

uniform sampler2D u_Texture;
uniform float u_BorderRadius;
uniform vec4 u_OverlayColor;

out vec4 color;

void main() {
    vec4 texture_color = texture(u_Texture, fragment_coords);
    vec4 overlay_color = u_OverlayColor;
    
    float texture_alpha = texture_color.a;
    float overlay_alpha = u_OverlayColor.a;
    
    float mixed_alpha = texture_alpha + overlay_alpha * (1.0 - texture_alpha);
    
    vec4 mixed_color;
    
    if (texture_alpha < 0.01) {
        mixed_color = texture_color;
    }
    else {
        mixed_color = mix(texture_color, overlay_color, overlay_alpha);
    }
    
    color = mixed_color;
}