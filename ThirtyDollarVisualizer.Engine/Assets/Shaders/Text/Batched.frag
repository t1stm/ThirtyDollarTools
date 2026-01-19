#version 330 core

uniform sampler2D msdf;
uniform vec4 uOutputColor;
uniform float uPxRange;

in vec2 fragTexCoord;
out vec4 outColor;

vec2 sqr(vec2 x) { return x * x; }

float screenPxRange() {
    vec2 unitRange = vec2(uPxRange) / vec2(textureSize(msdf, 0));

    vec2 screenTexSize = inversesqrt(sqr(dFdx(fragTexCoord)) + sqr(dFdy(fragTexCoord)));
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
}

float median(float r, float g, float b) {
    return max(min(r, g), min(max(r, g), b));
}

void main() {
    vec3 msd = textureLod(msdf, fragTexCoord, 0.0).rgb;
    float sd = median(msd.r, msd.g, msd.b);

    float screenPxDistance = (sd - 0.5) * screenPxRange();
    float opacity = clamp(screenPxDistance + 0.5, 0.0, 1.0);

    outColor = vec4(uOutputColor.rgb, uOutputColor.a * opacity);
}