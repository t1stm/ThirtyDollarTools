# Text Rendering

Sundex renders text with **[Slug](https://github.com/EricLengyel/Slug)**, Eric Lengyel's algorithm. The GPU computes each pixel's coverage directly from the glyph's quadratic Bézier outline. There's no distance field and no glyph bitmap, and the CPU rasterises nothing per glyph. Each font is built once into two float textures, and each `TextBuffer` draws all its characters as instanced quads in one call. Text is sharp at any size, and overlapping contours fill correctly by the non-zero rule.

> Source: `Sundex/Sundex.Engine/Text/` and `Sundex/Sundex.Slug/`. Shaders: `Assets/Shaders/Text/Batched.vert/.frag`. Full design and measurements: `docs/handover/sundex-slug-plan.md`.

## Pipeline

```
   font file
      │  SlugFont.Load (Sundex.Slug)
      ▼
   SlugFont.SlugData ── built once per font, on first use ──► SlugFontData
      │                                                        Curves  (RGBA32F, 4096 wide)
      │                                                        Bands   (RGBA32F, 4096 wide)
      │                                                        SlugGlyph per codepoint
      │
      │  TextProvider: two GPUTextures, uploaded once on first bind
      │
   TextSlice.UpdateCharacters ── GetGlyph(c) → SlugGlyph ── writes TextCharacter into ──► GLBuffer<TextCharacter>
                                                                                              │
                                                                                              │  VAO + GLQuad + Batched.vert/.frag
                                                                                              ▼
                                                                                          TextBuffer.Render(camera)
```

## `Sundex.Slug`: the font data

`SlugFont.Load(stream)` parses a font with SixLabors.Fonts. The first access to `font.SlugData` builds `SlugFontData` for every codepoint the font maps, plus `.notdef`:

- **Curves:** every glyph outline as quadratic Béziers, two texels per curve (`(p1, p2)` then `(p3, –)`). Consecutive curves of a contour share a texel. Lines are stored as quadratics; cubics are split into four quadratics.
- **Bands:** per glyph, 1–16 horizontal and 1–16 vertical bands. Each band lists the curves that cross it, sorted so the shader can stop early.
- **`SlugGlyph`:** `Advance`, `EmRect` (the ink box padded by 1/16 em), `Banding` and `GlyphData` (where its bands live), plus `TextTranslate`/`TextScale` for layout. Everything is in em space: Y-up, origin on the baseline.

A Lato face takes about 1.3 MiB of curves and 5.4 MiB of bands. The outlines are extracted in parallel and the build takes a few hundred ms, so the Loader runs it on a worker at boot (`Loader.WarmFonts`). `UIContext.Preload` starts the interface font's build even earlier, because the loading screen's own labels need it. After the build, `GetGlyph` is a dictionary lookup and is safe from any thread.

## `TextProvider`

```csharp
[PreloadGraphicsContext]
public class TextProvider(IAssetProvider provider, IFontProvider fontProvider, string fontName)
    : IGamePreloadable, ITextProvider
{
    public IGlyphProvider GlyphProvider { get; }
    public void BindAndSetUniforms(Camera camera);
}

public interface ITextProvider
{
    IGlyphProvider GlyphProvider { get; }
    SlugGlyph GetGlyph(ReadOnlySpan<char> character) => GlyphProvider.GetSlugGlyph(character);
    void BindAndSetUniforms(Camera camera);
}
```

There's one `TextProvider` per font. On its first `BindAndSetUniforms`, it creates two `GPUTexture { InternalFormat = Rgba32f, MipmapMode = Disabled }` from the font's `SlugFontData` and queues one upload each. Every bind after that:

```csharp
GL.ActiveTexture(TextureUnit.Texture1);
_bands.Bind();
GL.ActiveTexture(TextureUnit.Texture0);   // left active: everything else binds to unit 0
_curves.Bind();
_shader.Use();
_shader.SetUniform("uVPMatrix", camera.GetVPMatrix());
_shader.SetUniform("uCurves", 0);
_shader.SetUniform("uBands", 1);
```

This is the only place in the engine that uses a texture unit other than 0.

`GlyphProvider` holds the font name and hands out `GetSlugGlyph` and `GetFontMetrics`. Providers over the same `SlugFont` share one build.

## `TextCharacter`: the per-instance vertex

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextCharacter() : IGPUReflection, IPositionable
{
    public Vector4 EmRect;                 // location 1: (left, bottom, right, top) in ems
    public Vector3 Position { get; set; }  // location 2
    public Vector3 Scale    { get; set; }  // location 3
    public Vector4 Color    { get; set; }  // location 4
    public Vector4 ClipRect { get; set; }  // location 5: (left, top, right, bottom) UI units; all-zero = unclipped
    public Vector4 Banding;                // location 6: the shader's bandTransform
    public Vector4 GlyphData;              // location 7: band location and band counts
}
```

Each character on screen is one instance of the unit quad ([GLQuad](../Renderer/Buffers.md#glquad)). `SelfReflectToGL` pushes these attributes into the `VertexBufferLayout`, which is the [`IGPUReflection`](../Renderer/Abstractions.md#igpureflection) pattern.

## The shaders

- **`Batched.vert`** places the quad from `Position`/`Scale` and interpolates an em-space sample position across it from `EmRect`. The quad's UI-top edge maps to the em box's top, because UI is Y-down and em space is Y-up. `Banding` and `GlyphData` pass through `flat`.
- **`Batched.frag`** is a GLSL 330 translation of Lengyel's `SlugPixelShader.hlsl` (MIT, and his copyright header is kept). It casts one ray along x and one along y through the pixel, sums the signed coverage of the curves in the pixel's two bands, and combines the two. The output is `vec4(color.rgb, color.a * coverage)`. A per-glyph clip rect is tested first and discards outside fragments.

`Sundex.Slug.Tests/SlugReference.cs` is the same pixel function in C#, run on the CPU against a ground-truth rasteriser. A data bug shows up in a test, not on screen.

## Layout

`TextSlice.UpdateCharacters` asks the provider for each character's `SlugGlyph` and builds a `FlexLineItemPlacementLayout` from it (`Advance`, `TextTranslate`, `TextScale`, newlines). `FlexLinePositioningProvider<TextCharacter>.UpdatePositions` then places each quad at a relative size of 1. That lands it exactly on the padded ink box:

- left edge `= pen + (l − 1/16)·fontSize`
- width `= (r − l + 1/8)·fontSize`
- top edge `= baseline − (t + 1/16)·fontSize`, with the baseline at `cursorY + fontSize`

`TextSliceTests` pins this. Advances, and so all label widths and caret positions, come straight from the font's advance widths.

## `TextBuffer`

```csharp
public class TextBuffer : IRenderable, IClippable, IDisposable
{
    public readonly GLBuffer<TextCharacter>.WithCPUCache Characters;
    public readonly ITextProvider TextProvider;
    public Vector4i? ClipRect { get; set; }   // scissor-clip rect, UI/camera space; null = unclipped
}
```

A `TextBuffer` owns one GPU buffer holding every character of every slice in it. Slices are ranges inside `Characters`, and removed slices' ranges are reused.

The VAO has two buffers: the static unit quad (3 floats per vertex), and the per-character instance buffer laid out by `TextCharacter.SelfReflectToGL`. `RenderBuffer` binds the VAO, calls `TextProvider.BindAndSetUniforms`, and issues one `glDrawElementsInstanced` up to the highest allocated slot. Cleared slots have zero scale and draw nothing.

## `TextSlice`

A handle to a contiguous range inside `TextBuffer.Characters`, with `Value`, `FontSize`, `Color`, `Position` and `ClipRect`. Setting any of them calls `UpdateCharacters()`, unless `UpdateManually` is set to batch several changes. `Dispose()` zeroes the slots and frees the range.

Surrogate pairs are passed to `GetGlyph` as a two-character span, which decodes them as one codepoint. A codepoint the font lacks draws as its `.notdef` box.

## Threading

- **Building a font's data** runs on whichever thread first touches `SlugData`, behind a thread-safe `Lazy`. At boot, that's a worker.
- **`GetGlyph`** is a lookup and is safe from any thread.
- **The texture upload** is queued and happens on the first `Bind()`, on the render thread.
- **`TextSlice` mutation** is single-threaded, typically from `Scene.Update` or input handlers.

## Limits

- Colour emoji (COLR layers) aren't drawn; an emoji font's base outlines are empty. The plan's "Not in this plan" section sketches how to stack one Slug glyph per colour layer.
- The quad pad is a fixed 1/16 em, enough down to about 8 px text. Lengyel's `SlugDilate` is the fix if smaller text clips.
- Each `TextProvider` uploads its own texture pair, so two providers over one font hold two copies.

## Related

- [Textures](../Renderer/Textures.md): `GPUTexture` is the storage.
- [GLQuad](../Renderer/Buffers.md#glquad) supplies the geometry.
- [Shaders](../Renderer/Shaders.md): the `Batched` pair.
- The [`Label`](../../Components/Labels.md) component is the most common consumer.
