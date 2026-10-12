# Sundex.Slug.Tests

Tests for [`Sundex.Slug`](../../Sundex.Slug/README.md). xUnit v3.

```
dotnet test Sundex/Tests/Sundex.Slug.Tests
```

Everything here is self-contained. Tests assert against geometry that is known analytically,
against the fonts' own metric tables, or against a ground-truth rasteriser. The fonts are the ones
the engine ships (`Sundex.Engine/Assets/Fonts`), copied into the test output by the csproj and
opened through `TestFonts`. Nothing depends on stored reference images.

| File | What it pins |
|---|---|
| `CoordinateContractTests` | The space outlines are built in: em-normalised, Y-up, on the baseline. Descenders below it and `_` entirely below it catch a flipped Y. Contour winding catches a double reverse. Metrics are checked against the font tables. |
| `SlugReference` | Not a test: a line-by-line C# port of the reference pixel shader's `SlugRender`, the same function `Batched.frag` is in GLSL. |
| `SlugCoverageTests` | The reference shader, run on the CPU over the built data, against a non-zero winding rasteriser of the flattened outline. Covers printable ASCII in Lato Regular and Bold at 16 and 48 px/em, overlapping and nested contours both ways round, `.notdef`, blank glyphs, empty input and surrogate pairs. |
| `SlugDataTests` | The layout rules the shader relies on: no run crosses a row end, band lists are sorted farthest-first, lines never sit in bands parallel to them, and every curve is in every band it touches. Also checks that the vectorised band helpers match scalar maths, and logs each bundled font's build cost. |

The coverage tests can't see every data fault. Slug combines two rays with `min()`, which hides
a single ray's error away from an edge. `EveryCurveIsInEveryBandItTouches` is the check that
catches a dropped nearest curve.

The library's internals are visible here (`InternalsVisibleTo`), so tests can build `Shape`s
directly and feed them to the builder.
