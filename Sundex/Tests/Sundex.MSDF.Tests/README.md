# Sundex.MSDF.Tests

Tests for [`Sundex.MSDF`](../../Sundex.MSDF/README.md). xUnit v3.

```
dotnet test Sundex/Tests/Sundex.MSDF.Tests
```

Everything here is self-contained: it asserts against geometry that is known analytically, or
against the fonts' own metric tables. The fonts are the ones the engine ships
(`Sundex.Engine/Assets/Fonts`), copied into the test output by the csproj and opened through
`TestFonts`. Nothing depends on stored reference images.

| File | What it pins |
|---|---|
| `CoordinateContractTests` | The space outlines are built in: em-normalised, Y-up, sitting on the baseline. Descenders below it and `_` entirely below it catch a flipped Y; contour winding catches a double-reverse; metrics are checked against the font tables. |
| `DistanceCoreTests` | Signed distances on a unit square and a four-cubic circle, at points whose answers are known by hand — inside, outside, on an edge, past a corner. Plus the quadratic and cubic root finders, including the three-real-roots and double-root branches. |
| `BlankGlyphTests` | A glyph that exists but has no ink — space, no-break space — still generates and still advances the pen, instead of being treated as a missing glyph. |
| `LifetimeTests` | Disposing a font is final for generation and harmless to repeat, and leaves its metrics readable. |

The library's internals are visible here (`InternalsVisibleTo`), so tests can drive `Shape`,
`EdgeSegment` and the solvers directly rather than only through the public API.
