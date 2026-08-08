# Text Rendering

Sundex renders text using **MSDF (Multi-channel Signed Distance Field)** glyphs. Every glyph the application uses is rasterised once at 48×48, packed into a single 2048×2048 `Rgba32f` atlas, and drawn via instanced quads with a small fragment shader that decodes the distance field. The result is crisp text at any zoom level with a single draw call per `TextBuffer`, no font hinting, no per-frame CPU rasterisation.

> Source: `Sundex/Sundex.Engine/Text/`. Shaders: `Assets/Shaders/Text/Batched.vert/.frag`.

## Pipeline

```
   font file
      │
      │  GlyphProvider (per missing character)
      ▼
   Msdfgen.Shape ── normalise, orient, edge-colour ──► MTSDF bitmap (48×48 RgbaVector)
      │
      │  GuillotineAtlas.AddImage  +  GPUTexture.QueueUploadToGPU
      ▼
   GPUTextureAtlas (2048×2048 Rgba32f)
      ▲
      │  TextProvider.GetTextCharacterRect("a") returns (atlas-rect, alignment-data)
      │
   TextSlice.UpdateCharacters  ──── writes per-instance TextCharacter into ────►  GLBuffer<TextCharacter>.WithCPUCache
                                                                                      │
                                                                                      │  VAO + Quad + Batched.vert/.frag
                                                                                      ▼
                                                                                  TextBuffer.Render(camera)
```

## `TextProvider`

```csharp
[PreloadGraphicsContext]
public class TextProvider(IAssetProvider provider, IFontProvider fontProvider, string fontName)
    : IGamePreloadable, ITextProvider
{
    private static Shader _shader = null!;

    public readonly GPUTextureAtlas TextAtlas = new(2048, 2048,
        InternalFormat.Rgba32f, MipmapMode.Disabled)
    { AtlasID = "TextAtlas_" + fontName.Replace(' ', '_') };

    public IGlyphProvider GlyphProvider { get; } = new GlyphProvider(fontProvider, fontName);

    public static void Preload(AssetProvider assetProvider) {
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Text/Batched");
    }

    public (Vector4, TextAlignmentData) GetTextCharacterRect(ReadOnlySpan<char> character);
    public void BindAndSetUniforms(Camera camera);
}
```

One `TextProvider` per font. The atlas is `Rgba32f` because MSDF data is float-valued (signed distances need negative values) and `Disabled` mipmaps because the shader does its own anti-aliasing in the distance domain — mipmapping a distance field would average distances and produce wrong results.

### Lazy glyph generation

`GetTextCharacterRect("a")` is the lazy entry point:

1. Lock the atlas, look up the existing rectangle for that character.
2. If empty, synthesise the glyph via `GlyphProvider.GetGlyph("a")` — which generates a 48×48 MTSDF bitmap, packs it into the atlas, and queues an upload to the texture.
3. Return `(uv-rect, sizing-data)`.

Glyphs are never evicted — once a character is drawn, its slot stays. The 2048×2048 atlas is intentionally oversized so any reasonable application's character set fits without churn.

### `BindAndSetUniforms`

Called once per `TextBuffer.Render(camera)`:

```csharp
TextAtlas.Bind();
_shader.Use();
_shader.SetUniform("uVPMatrix", camera.GetVPMatrix());
_shader.SetUniform("uPxRange", GlyphProvider.GlyphSize * GlyphProvider.MsdfRange);  // 48 * 4 = 192
```

`uPxRange` is the screen-space distance scale — it tells the fragment shader how to map the unit-space distance values to pixel widths for anti-aliasing.

## `GlyphProvider` — the MSDF generator

```csharp
public class GlyphProvider(IFontProvider fontProvider, string fontName) : IGlyphProvider
{
    public const int   GlyphSize = 48;
    public const float MsdfRange = 4.0f;

    public Image<RgbaVector> GetGlyph(ReadOnlySpan<char> character);
    public TextAlignmentData GetSizingData(ReadOnlySpan<char> character);
    public FontMetrics       GetFontMetrics();
}
```

The interesting work happens in `GetGlyph`:

1. **Read the codepoint** — `MemoryMarshal.AsBytes(character).CopyTo(MemoryMarshal.AsBytes(uintSpan))`. Surrogate pairs are stored as a `uint`.
2. **Load the glyph contour** via `Msdfgen.FontLoader.LoadGlyph` — uses the font's em-normalised coordinate system, returns the advance width.
3. **Validate / normalise / orient** — `shape.Validate()`, `shape.Normalize()`, `FixGeometry(shape)` (reverses contours if the glyph is "outside in"), then orient + normalise again.
4. **Auto-frame** the shape into the 48×48 bitmap, leaving room for the `pxRange = 4` padding band on each side.
5. **Edge-colour** the shape with a 3° angle threshold — assigns red/green/blue channels to different edge directions for the multi-channel SDF trick.
6. **Generate** with `MsdfGenerator.GenerateMTSDF` into a `Bitmap<float>` (4 channels — R, G, B for the multi-channel distances, A for the true distance).
7. **Repack** into a `RgbaVector[]` and wrap as `Image<RgbaVector>` for ImageSharp / `GPUTexture` consumption.
8. Cache the alignment data (`advance`, `scale`, `translate`) in `SizingData`.

The font handle is **not cached** — the comment in source explains: *"Font cannot be a cached due to some constraint with the underlying library / unmanaged C# probably disposing the handle somewhere in the code."* Each glyph generation re-opens the font via `FontProvider`. Cheap enough since `FontMetrics` itself is cached.

## `TextCharacter` — the per-instance vertex

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextCharacter() : IGPUReflection, IPositionable
{
    public Vector4 TextureUV;
    public Vector3 Position { get; set; }
    public Vector3 Scale    { get; set; }
    public Vector4 Color    { get; set; } = Vector4.One;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(4, true);  // TextureUV
        layout.PushFloat(3, true);  // Position
        layout.PushFloat(3, true);  // Scale
        layout.PushFloat(4, true);  // Color
    }
}
```

Each character on screen is one instance of a unit quad ([GLQuad](../Renderer/Buffers.md#glquad)) with these per-instance attributes. `TextureUV` is normalised to atlas coordinates `(u0, v0, u1, v1)`. `Position` and `Scale` come from the layout pass.

`SelfReflectToGL` pushes its own attributes into the `VertexBufferLayout` — this is the [`IGPUReflection`](../Renderer/Abstractions.md#igpureflection) pattern in action.

## `TextBuffer`

```csharp
public class TextBuffer : IRenderable, IClippable, IDisposable
{
    public readonly GLBuffer<TextCharacter>.WithCPUCache Characters;
    public readonly ITextProvider TextProvider;
    private readonly VertexArrayObject _vao = new();
    private readonly List<Range> _freeRanges = [];
    private readonly Dictionary<TextSlice, Range> _usedRanges = [];
    private int _currentOffset;

    public Vector4i? ClipRect { get; set; }   // scissor-clip rect, UI/camera space; null = unclipped
}
```

A `TextBuffer` owns a single GPU buffer holding *every* character of *every* slice that lives in it. Slices are allocated as ranges inside `Characters`, with free-range tracking so removed slices' slots can be reused.

### VAO setup

```csharp
vao.AddBuffer(GLQuad.VBOWithoutUV, new VertexBufferLayout().PushFloat(3));

var layout = new VertexBufferLayout();
TextCharacter.SelfReflectToGL(layout);
vao.AddBuffer(Characters, layout);

vao.SetIndexBuffer(GLQuad.EBO);
```

Two VBOs: the static unit quad (3 floats per vertex) and the dynamic per-character instance buffer. The fragment shader takes the quad's per-vertex UV (0..1), interpolates it, and uses `TextureUV` to remap into atlas coordinates.

### Slice allocation

`GetTextSlice(text, capacity)`:

1. If the buffer doesn't have room for `_currentOffset + capacity`, resize the CPU + GPU buffers.
2. Look for a free range with enough length; split it if it's larger than needed.
3. Otherwise, append at `_currentOffset` and bump.
4. Hand back a `TextSlice` that knows its `Offset` and `Length`.

### Render

```csharp
public void RenderBuffer(Camera camera, int endIndex = -1)
{
    if (endIndex < 0) endIndex = _currentOffset;
    _vao.Bind();
    _vao.Update();
    TextProvider.BindAndSetUniforms(camera);
    GLQuad.DrawInstanced(endIndex);
}
```

One `glDrawElementsInstanced` call covers the entire buffer. `endIndex` defaults to `_currentOffset`, which means *only the slots up to the highest-watermark allocation* are drawn; cleared slots at indices < `_currentOffset` carry zero `Color.A` so they're transparent.

## `TextSlice`

A logical handle to a contiguous range inside `TextBuffer.Characters`:

```csharp
public class TextSlice(TextBuffer textBuffer, Range range) : IPositionable, IDisposable
{
    public ReadOnlySpan<char> Value { get; set; }
    public float FontSize { get; set; }
    public Vector4 Color  { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Scale    { get; set; }
    public bool UpdateManually { get; set; }
}
```

Setting `Value`, `FontSize`, or `Position` calls `UpdateCharacters()` — which walks the string, asks the `TextProvider` for each character's atlas rectangle and alignment data, builds a `FlexLineItemPlacementLayout` describing wrapping/advance, and writes one `TextCharacter` per character into `textBuffer.Characters[Offset + i]`.

The actual *positioning* is done by `FlexLinePositioningProvider<TextCharacter>.UpdatePositions` — flex-line layout that supports newlines, font-metric-based line-height, em-relative scaling, and optional character-by-character overrides. The provider mutates the slice of `TextBuffer.Characters` in place.

`UpdateManually = true` defers the rebuild — useful when you're about to set multiple properties at once and don't want N redundant rebuilds.

`Dispose()` calls `textBuffer.Remove(this)`, which zeroes out the character slots and adds the range to `_freeRanges` for reuse.

### Surrogate pairs

`UpdateCharacters` handles UTF-16 surrogate pairs explicitly:

```csharp
if (char.IsSurrogate(character) &&
    char.IsSurrogatePair(character, val[index + 1])) {
    characters[0] = character;
    characters[1] = val[index + 1];
    rect = textProvider.GetTextCharacterRect(characters);
    index++;
}
```

The `GetTextCharacterRect` overload takes a `ReadOnlySpan<char>` of length 1 or 2, so emoji and other supplementary-plane glyphs work without special-casing in the atlas.

## Layout helpers

`FlexLinePositioningProvider<T>` and `FlexLineItemPlacementLayout` live in the same folder. They turn a sequence of `(advance, translate, scale, newLines)` records into actual `TextCharacter.Position` values, accounting for line-height (from font metrics), em-size, and the slice's font-size scale. The result is also returned as a `Vector2` size that becomes `slice.Scale.XY` — useful for caller-side bounding boxes.

## The `Allocationless/` folder

A handful of helpers for zero-allocation iteration over text — typically used by [labels and buttons](../../Components/Components.md) during input handling, where they need to walk character positions to find click targets without GC pressure.

## Threading

- **Glyph generation** (`GlyphProvider.GetGlyph`) runs on whichever thread invoked it, but is locked behind the atlas lock so two concurrent loads of the same character won't collide. The MSDF generation is pure CPU.
- **Atlas upload** (`GPUTexture.QueueUploadToGPU`) is queued; the actual GL upload happens on next `Bind()` (i.e. next render frame).
- **`TextSlice` mutation** is single-threaded — typically from `Scene.Update` or input handlers.

## Related

- [GPUTextureAtlas](../Renderer/Textures.md#atlases) is the storage substrate.
- [GLQuad](../Renderer/Buffers.md#glquad) supplies the geometry.
- [Batched shader](../Renderer/Shaders.md) decodes the MSDF in the fragment stage.
- [CacheProvider](../Asset%20Management.md#the-cache) is what makes the atlas survive across runs (see `GPUTextureAtlas.LoadFromCache`).
- The [`Label`](../../Components/Labels.md) component is the most common consumer.
