# Sundex.MSDF

Generates multi-channel signed distance field (MSDF) bitmaps for glyphs, so text can be drawn from one texture atlas at
any size and stay sharp at the corners.

Outlines come from [SixLabors.Fonts](https://github.com/SixLabors/Fonts); the distance-field algorithms
follow [Chlumsky/msdfgen](https://github.com/Chlumsky/msdfgen) (MIT). There is no native dependency — an `MsdfFont` is
an ordinary managed object.

## Using it

```csharp
using Sundex.MSDF;

using var font = MsdfFont.Load(File.OpenRead("Lato-Regular.ttf"));

const int size = 48;
var pixels = new float[size * size * 4];           // RGBA; A is never written
pixels.AsSpan().Fill(1f);                          // so leave alpha at 1 yourself

if (font.TryGenerate("A", pixels, size, channels: 4, pxRange: 4.0, out var glyph))
{
    // glyph.Advance / glyph.Translate / glyph.Scale place the bitmap — see below
}
```

`TryGenerate` writes three channels with the stride you ask for, so you can generate straight into an RGBA buffer
without a second copy. It returns false only when the input decodes to no codepoint at all; a character the font does
not cover still generates, as that font's `.notdef`
box, and a character with no ink (space) generates a field that is everywhere outside.

`MsdfFont.Metrics` gives the font-wide vertical metrics in raw font units. Line spacing is the ratio
`LineHeight / EmSize`.

## The coordinate contract

Everything the caller gets back is in **em space: one unit per em, Y-up, origin on the baseline**.

- `Advance` — how far to move the pen, in ems.
- `Translate` and `Scale` — the transform used to fit the outline into the square bitmap. The glyph's ink does not fill
  that square, so these are needed to place it; the shape is centred on its shorter axis with `pxRange` pixels of margin
  for the field to fall off in.

Two things about the output buffer are easy to trip over:

- **Row 0 is the bottom of the glyph.** From an image's point of view the buffer is upside down. That is deliberate — an
  OpenGL texture's V axis runs bottom-up and flips it back on sampling.
- **`pxRange` is in shape units, not output pixels.** The engine passes `4.0` against an em-normalised shape, so a
  glyph's field only occupies a narrow band around 0.5, and the shader stretches it back out with `uPxRange`. The two
  are coupled: change one without the other and the antialiasing width changes with it. In the engine,
  `uPxRange = GlyphSize * MsdfRange`.

## Ownership, threading and allocation

**The library holds no state of its own.** `Load` parses a font and hands it back; where it lives and how long it lives
are the caller's business, and there is no shared cache behind it — load the same file twice and you get two independent
fonts, each having re-parsed it.

The reusable scratch that keeps glyph generation allocation-free — the shape being built, the distance finders — hangs
off the `MsdfFont` that uses it, not off a static or a `[ThreadStatic]`
field. So a consumer that generates a few glyphs and drops the font gets all of that memory back, and one that keeps a
font keeps exactly what it is using. The only shared thing touched is
`ArrayPool<T>.Shared`, which is the framework's own and trims itself.

`MsdfFont` implements `IDisposable` so that scratch can be released at a point you choose —
`using var font = …` suits a tool that renders one atlas and exits, while an application that draws text for its whole
run just holds the font and disposes it at shutdown, or not at all. Disposing is **optional**: nothing unmanaged is
behind it, so a dropped font leaks nothing. Afterwards `Metrics` still reads (it is captured at load) but `TryGenerate`
throws
`ObjectDisposedException`. Don't dispose a font while another thread is still generating from it.

`TryGenerate` is safe to call concurrently on a shared `MsdfFont`; scratch is borrowed per call rather than locked. The
per-pixel loop is split into row bands across the thread pool; bands are independent, so banded output is bit-identical
to serial.

## Layout

|                                      |                                                                              |
|--------------------------------------|------------------------------------------------------------------------------|
| `MsdfFont`                           | The whole public API: load a font, generate a glyph.                         |
| `MsdfGlyph`, `Fonts/MsdfFontMetrics` | What comes back out.                                                         |
| `Fonts/OutlineGlyphRenderer`         | SixLabors outline callbacks → `Shape`.                                       |
| `Geometry/`                          | Vectors, edge segments and their distance maths, contours, polynomial roots. |
| `Coloring/`                          | Assigns each edge two of the three channels, switching at corners.           |
| `Distance/`                          | The per-point distance search: selectors, edge cache, shape walk.            |
| `Generation/`                        | Projection, distance mapping, and the per-pixel rasteriser.                  |

Only `MsdfFont`, `MsdfGlyph` and `MsdfFontMetrics` are public; everything else is `internal` and visible to the test
project.

## Not implemented

Deliberate omissions, each marked with a `ponytail:` comment where it would go:

- **Error correction.** The pass that hunts isolated pixels whose median disagrees with their neighbours. The wide
  distance range in use makes a smooth enough field not to need it — revisit if speckles appear or the range is
  tightened.
- **Overlapping-contour combining.** One selector takes all contours together, which is exact only where a glyph's
  contours do not overlap. The selectors already implement `Merge`, so it is a per-contour selector array away.
- **Contour re-orientation.** Winding is fixed for the shape as a whole, not per contour, so a font whose holes are
  wound like their outlines would render with filled counters.
- **Arcs** (SVG-in-OpenType) are chorded to straight lines, and **colour layers** (COLR/CPAL) are ignored in favour of
  the base outline — which for an emoji font is usually empty.
- Only MSDF, three channels. No SDF/PSDF, no true-distance fourth channel, no image I/O, no atlas packing.

## Tests

`Sundex/Tests/Sundex.MSDF.Tests` — see its own README.

```
dotnet test Sundex/Tests/Sundex.MSDF.Tests
```
