#version 330 core

in vec2 fragTexCoord;
in vec4 fragColor;
flat in uint fragTexIndex;

uniform sampler2DArray uTextureArray;
uniform float atlasSize;

out vec4 outColor;

void main() {
    if (fragTexIndex < 1u) {
        outColor = fragColor;
    }
    else {
        outColor = texture(uTextureArray, vec3(fragTexCoord, float(fragTexIndex - 1u)));
    }
}
