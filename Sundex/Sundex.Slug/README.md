# Sundex.Slug

Turns a font into the curve and band data the [Slug](https://github.com/EricLengyel/Slug)
algorithm renders text from. The GPU then works out each pixel's coverage straight from the
glyph's quadratic Bézier outline, so text is sharp at any size from one pair of textures per
font, with no glyph bitmaps and no per-glyph CPU work after the build.

Slug is by Eric Lengyel. The engine's `Text/Batched.frag` is a GLSL translation of his reference
`SlugPixelShader.hlsl` (MIT, Copyright 2017 Eric Lengyel), and this library builds the data in the
layout his shaders expect. The algorithm's patent was dedicated to the public domain on
2026-03-17. Outlines come from [SixLabors.Fonts](https://github.com/SixLabors/Fonts). There's no
native dependency, and nothing here touches GL.

## Using it

```csharp
using Sundex.Slug;

var font = SlugFont.Load(File.OpenRead("Lato-Regular.ttf"));
var data = font.SlugData;                 // built on first access, a few hundred ms

// Upload these as two RGBA32F textures, SlugFontData.TextureWidth texels wide:
//   data.Curves (data.CurveRows rows) and data.Bands (data.BandRows rows).
SlugGlyph glyph = data.GetGlyph("A");     // surrogate pairs work; a missing codepoint gets .notdef
```

A `SlugGlyph` holds everything one glyph's quad needs:

- `Advance`: how far to move the pen, in ems.
- `EmRect`: the quad's corners in em space, `(left, bottom, right, top)`. This is the ink
  bounds grown by `SlugFontData.QuadPadding` (1/16 em) on every side, because Slug
  anti-aliases a half-pixel fringe outside the ink.
- `Banding` and `GlyphData`: the shader's `bandTransform` and `glyphData`.
- `TextTranslate` and `TextScale`: the same box, as the translate/scale pair the engine's line
  layout places quads with.

`SlugFont.Metrics` gives the font's vertical metrics in raw font units. Line spacing is
`LineHeight / EmSize`.

## The coordinate contract

Everything is in **em space: one unit per em, Y-up, origin on the baseline**. A glyph's outline
sits at the height the font's own metrics give it (`Ascender − TopSideBearing`), not where
SixLabors' line box would put it. `CoordinateContractTests` pins this.

## The data

The layout follows Lengyel's reference shaders. `docs/handover/sundex-slug-plan.md` ("Data
layout") is the full spec. In short:

- **Curves:** each quadratic is two texels, `(p1, p2)` then `(p3, –)`. Consecutive curves of a
  contour share the texel between them. Lines are stored as quadratics with the end point
  doubled. Cubics are split into four quadratics.
- **Bands:** per glyph, its horizontal then vertical band headers `(count, offset)`, then each
  band's list of curve locations, sorted by how far the curves reach along the ray. Each axis
  gets 1–16 bands, whichever count keeps the longest list shortest.
- **Nothing crosses a row end:** a contour's curve run, a glyph's header run and a band's list
  all stay within one texture row. The shader reads them without wrapping.

## Ownership and threading

`SlugFont.Load` parses the font, and the caller keeps the result. There's no shared cache
behind it: loading the same file twice gives two fonts, each building its own data. Nothing
unmanaged is held.

`SlugData` is built once, on first access, behind a thread-safe `Lazy`. The outlines are
extracted in parallel on up to half the machine's cores, then packed in order. The band-count
search, the build's hottest loop, runs `Vector<double>`-wide. After that, `GetGlyph` is a
dictionary lookup and safe from any thread. Time the build in Release: Debug doesn't vectorise.

## Layout

|                              |                                                               |
|------------------------------|---------------------------------------------------------------|
| `SlugFont`                   | Load a font; its metrics, outlines and `SlugData`.            |
| `SlugFontData`, `SlugGlyph`  | The curve and band textures, and the per-glyph record.        |
| `Fonts/SlugFontMetrics`      | Font-wide vertical metrics.                                   |
| `Fonts/OutlineGlyphRenderer` | Turns SixLabors' outline callbacks into a `Shape`.            |
| `Geometry/`                  | Vectors, edge segments, contours, and the bounds maths.       |

`SlugFont`, `SlugFontData`, `SlugGlyph` and `SlugFontMetrics` are public. Everything else is
`internal` and visible to the test project.

## Not implemented

These are deliberate. Each has a `ponytail:` comment where it would go:

- **Dynamic dilation** (the reference `SlugDilate`). The quad pad is a fixed 1/16 em, enough
  down to about 8 px text.
- **Error-bounded cubic splitting.** Cubics become four quadratics by midpoint approximation.
  Every bundled font is TrueType, so no cubics arrive today.
- **Arcs** (SVG-in-OpenType) are chorded to straight lines.
- **Colour layers** (COLR/CPAL) are ignored in favour of the base outline, which for an emoji
  font is usually empty.
- **Smaller textures:** half-float curves, integer bands, and sharing identical band lists.
  They'd only save memory.

## Tests

`Sundex/Tests/Sundex.Slug.Tests`. See its own README.

```
dotnet test Sundex/Tests/Sundex.Slug.Tests
```
