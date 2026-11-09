#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aTranslateXYZ;
layout (location = 2) in vec2 aScale;
layout (location = 3) in vec4 aColor;
layout (location = 4) in vec4 aClipRect;
layout (location = 5) in uint aGlyphIndex; // TextCharacter.Glyph

#define kLogGlyphTextureWidth 12
#define kTexelsPerGlyph 3 // SlugFontData.TexelsPerGlyph

uniform mat4 uVPMatrix;
// SlugFontData.Glyphs: per glyph, its em rect, banding and glyph data, one texel each.
uniform sampler2D uGlyphs;
// Em-space sample position (Y-up, origin on the baseline), what the Slug pixel shader
// evaluates coverage at.
out vec2 vEmCoord;
out vec4 vColor;
// UI-space position of this fragment, and the box it is confined to. The glyph's
// transform is applied before uVPMatrix, so this stage already holds the same
// absolute UI units ClipRect is written in - no viewport size or gl_FragCoord
// unflipping needed downstream. flat: one value per instance, never interpolated.
out vec2 vUIPosition;
flat out vec4 vClipRect;
flat out vec4 vBanding;
flat out vec4 vGlyph;

vec4 GlyphTexel(int texel) {
    int index = int(aGlyphIndex) * kTexelsPerGlyph + texel;
    return texelFetch(uGlyphs, ivec2(index & ((1 << kLogGlyphTextureWidth) - 1), index >> kLogGlyphTextureWidth), 0);
}

void main() {
    vec4 emRect = GlyphTexel(0);

    mat4 transformMatrix = mat4(
    aScale.x, 0.0, 0.0, 0.0,
    0.0, aScale.y, 0.0, 0.0,
    0.0, 0.0, 1.0, 0.0,
    aTranslateXYZ.x, aTranslateXYZ.y, aTranslateXYZ.z, 1.0
    );

    vec4 ui_coords = transformMatrix * vec4(aPosition.xyz, 1.0);
    vec4 final_coords = uVPMatrix * ui_coords;

    vUIPosition = ui_coords.xy;
    vClipRect = aClipRect;
    vColor = aColor;
    vBanding = GlyphTexel(1);
    vGlyph = GlyphTexel(2);

    // The unit quad's y = 0 is its top edge in the Y-down UI, and the em box's top is its
    // largest y, so y flips here. emRect is (left, bottom, right, top).
    vEmCoord = vec2(mix(emRect.x, emRect.z, aPosition.x), mix(emRect.w, emRect.y, aPosition.y));
    gl_Position = final_coords;
}
