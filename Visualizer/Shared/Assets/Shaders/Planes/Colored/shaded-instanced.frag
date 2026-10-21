#version 330 core

// The editor's LineBatch rects (see ShadedRect): a flat fill unless the instance carries a
// shape - rounded corners, an outline in place of the fill, and a light over it. Everything is
// measured in px from the box's own edges, so a rect looks the same wherever it sits.

flat in vec4 vRGBA;
flat in vec4 vShape;
flat in vec2 vSize;
in vec2 vLocal;

out vec4 color;

// Signed distance to a rounded box, negative inside.
float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p) - b + r;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
}

// The band an "inset 0 dy 0" box shadow paints: inside the box, outside the same box moved
// down by dy. A positive dy is the band along the top edge, a negative one the bottom's.
float insetBand(vec2 p, vec2 b, float r, float dy, float pixel) {
    return clamp(0.5 + roundedBoxSDF(p - vec2(0.0, dy), b, r) / pixel, 0.0, 1.0);
}

void main() {
    color = vRGBA;
    // Taken before any branch: derivatives are only defined in uniform control flow.
    vec2 fw = fwidth(vLocal);
    if (vShape == vec4(0.0)) return;

    float pixel = max(max(fw.x, fw.y), 1e-4);
    vec2 halfSize = vSize * 0.5;
    int bits = int(vShape.y);
    // The quarter this fragment is in picks its corner: 1 top-left, 2 top-right,
    // 4 bottom-right, 8 bottom-left. A corner left out of the mask stays square.
    int corner = vLocal.y < 0.0 ? (vLocal.x < 0.0 ? 1 : 2) : (vLocal.x < 0.0 ? 8 : 4);
    float radius = (bits & corner) != 0 ? min(vShape.x, min(halfSize.x, halfSize.y)) : 0.0;

    // Coverage centred on the edge, one pixel wide - the same as colored.frag's.
    float dist = roundedBoxSDF(vLocal, halfSize, radius);
    float alpha = clamp(0.5 - dist / pixel, 0.0, 1.0);
    // An outline keeps only the band inside the edge, dist in [-width, 0].
    if (vShape.z > 0.0) alpha *= clamp(0.5 + (dist + vShape.z) / pixel, 0.0, 1.0);
    color.a *= alpha;

    float s = vShape.w;
    if (s <= 0.0) return;

    vec3 c = color.rgb;
    float top = vLocal.y + halfSize.y;
    float t = top / vSize.y;
    int light = (bits >> 4) & 3;
    if (light == 0) {
        // A note: white fading out by 55 % of the height, black growing in below it, a 2 px
        // lip along the bottom of a body tall enough to spare it, and a 1 px top highlight.
        c = mix(c, vec3(1.0), 0.16 * s * clamp(1.0 - t / 0.55, 0.0, 1.0));
        c = mix(c, vec3(0.0), 0.12 * s * clamp((t - 0.55) / 0.45, 0.0, 1.0));
        if (vSize.y >= 8.0) c = mix(c, vec3(0.0), 0.30 * s * insetBand(vLocal, halfSize, radius, -2.0, pixel));
        c = mix(c, vec3(1.0), 0.28 * s * insetBand(vLocal, halfSize, radius, 1.0, pixel));
    } else if (light == 1) {
        // A keyframe marker: lifted at the top, sunk at the bottom, and the same 2 px lip.
        c = mix(mix(c, vec3(1.0), 0.18 * s), mix(c, vec3(0.0), 0.08 * s), t);
        c = mix(c, vec3(0.0), 0.30 * s * insetBand(vLocal, halfSize, radius, -2.0, pixel));
    } else if (light == 2) {
        // A clip's body: a 3 px lip along its bottom.
        c = mix(c, vec3(0.0), 0.30 * s * insetBand(vLocal, halfSize, radius, -3.0, pixel));
    } else {
        // A clip's name strip: a white light fading out by 18 px, and the clip's 1 px top highlight.
        c = mix(c, vec3(1.0), 0.14 * s * clamp(1.0 - top / 18.0, 0.0, 1.0));
        c = mix(c, vec3(1.0), 0.30 * s * insetBand(vLocal, halfSize, radius, 1.0, pixel));
    }

    color.rgb = c;
}
