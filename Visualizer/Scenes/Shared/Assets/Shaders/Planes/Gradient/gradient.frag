#version 330 core
#define MAX_STOPS 8

#define PI 3.14159265359

precision highp float;
out vec4 color;

in vec2 vLocalPos;
in vec2 vUV;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;
    vec4 u_ScaleAndBorderPx;

    int u_GradientType;
    int u_GradientStopCount;

    vec4 u_GradientColors[MAX_STOPS];
    float u_GradientStops[MAX_STOPS];
};

vec4 getGradientColor(float t) {
    if (u_GradientStopCount <= 0) return vec4(0.0);
    if (u_GradientStopCount == 1) return u_GradientColors[0];

    t = clamp(t, 0.0, 1.0);

    if (t <= u_GradientStops[0]) return u_GradientColors[0];
    if (t >= u_GradientStops[u_GradientStopCount - 1]) return u_GradientColors[u_GradientStopCount - 1];

    for (int i = 0; i < MAX_STOPS - 1; i++) {
        if (i >= u_GradientStopCount - 1) break;

        float lower = u_GradientStops[i];
        float upper = u_GradientStops[i + 1];

        if (t <= upper) {
            float diff = upper - lower;
            float factor = diff > 0.0 ? (t - lower) / diff : 0.0;
            return mix(u_GradientColors[i], u_GradientColors[i + 1], factor);
        }
    }
    return u_GradientColors[u_GradientStopCount - 1];
}

float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p) - b + r;
    return length(max(d, 0.0)) - r;
}

/* Gradient noise from Jorge Jimenez's presentation: */
/* http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare */
float gradientNoise(in vec2 uv)
{
    return fract(52.9829189 * fract(dot(uv, vec2(0.06711056, 0.00583715))));
}

void main() {
    float t = 0.0;

    if (u_GradientType == 0) {
        color = u_GradientColors[0];
    }
    else {
        if (u_GradientType == 1) { 
            // linear
            t = vUV.x;
        }
        else if (u_GradientType == 2) { 
            // radial
            vec2 halfSize = u_ScaleAndBorderPx.xy * 0.5;
            vec2 normalized = vLocalPos / halfSize;
            t = length(normalized);
        }
        else if (u_GradientType == 3) { 
            // conical
            t = atan(vLocalPos.y, vLocalPos.x) / (2.0 * PI) + 0.5;
        }
        color = getGradientColor(t);
    }

    // Rounded corners
    float borderRadius = u_ScaleAndBorderPx.z;
    if (borderRadius > 0.0) {
        float dist = roundedBoxSDF(vLocalPos, u_ScaleAndBorderPx.xy * 0.5, borderRadius);
        float edge = fwidth(dist);
        float alpha = 1.0 - smoothstep(0.0, edge, dist);
        color.a *= alpha;
    }
    
    float noise = gradientNoise(gl_FragCoord.xy);
    float dither = (1.0 / 255.0) * gradientNoise(gl_FragCoord.xy) - (0.5 / 255.0);
    color.rgb += dither;
}