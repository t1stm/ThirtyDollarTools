# Syntax

The lexical layer of the [[Style DSL|Style DSL]] — characters, identifiers, numbers, strings, hex colors, comments. Everything here is implemented in `StyleParser.cs` as a hand-rolled recursive-descent parser over a `string dsl`.

> Source: `Sundex/Sundex.Style.DSL/StyleParser.cs`.

## At a glance

| Construct | Example | Notes |
|---|---|---|
| Identifier | `font-size`, `border-radius`, `button1` | Letters, digits, `-`. No leading digit. |
| Number | `14`, `14px`, `0.5`, `90deg`, `100%`, `-3` | Optional unit suffix. |
| String | `"hello"`, `"#ff0000"` | Double-quoted only. Hex strings are special-cased. |
| Hex color | `#ff0000`, `#ff0000aa` | 6 or 8 hex digits. |
| Keyword | `!gradient`, `!override`, `!keyframes`, `!stops`, `!direction` | Prefixed with `!`. |
| Vector | `vec2(10, 20)`, `vec3(1, 0, 0)`, `vec4(...)` | Parens and commas. |
| Block | `{ key = value; }` | Properties separated by `;` or `,`. |
| Array | `[ value1, value2 ]` | Comma-separated. |
| Map | `[ key1 = value1, key2 = value2 ]` | Same brackets, but with `=`. |
| Comment | `// ...` | Single-line only. To end of line. |
| Whitespace | spaces, tabs, newlines | Insignificant outside strings. |

## The parser shape

```csharp
public class StyleParser(
    string dsl,
    Func<string, string>? fileLoader = null,
    HashSet<string>? importedPaths = null)
{
    private readonly HashSet<string> _importedPaths = importedPaths ?? [];
    private int _pos;

    public static StyleSheetHolder Parse(string dsl, Func<string, string>? fileLoader = null) {
        var parser = new StyleParser(dsl, fileLoader);
        return parser.ParseSheet();
    }
    ...
}
```

A primary-constructor class with three parameters:

- `dsl` — the source text.
- `fileLoader` — optional `(string path) => string content` callback for `import` directives. Null disables imports.
- `importedPaths` — for cycle protection. Inner parsers (one per imported file) inherit this set so the same file can't be re-imported recursively.

`_pos` is the cursor position — character index into `dsl`. Single-pass forward scan; no lookahead beyond `PeekNext()`.

## Identifiers

```csharp
private string ReadIdentifier() {
    var start = _pos;
    while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '-')) Advance();
    return dsl[start.._pos];
}
```

Letters, digits, and hyphens. **No leading character restriction enforced** — practically, identifiers start with a letter because the parser uses identifier-reading only after a known starting character. Hyphens are part of the identifier, which is why `font-size`, `border-radius`, `timing-function` work without quotes.

The trade-off: `font-size = 14 - 2;` would parse as identifier `font-size`, `=`, identifier `14` (number), identifier `-`... actually it wouldn't — numbers consume `-` as a sign prefix only at the start. There's no arithmetic in the DSL, so the ambiguity never arises.

## Numbers

```csharp
private NumberValue ParseNumber() {
    var start = _pos;
    if (Peek() == '-') Advance();
    while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.')) Advance();
    var numStr = dsl[start.._pos];
    if (!float.TryParse(numStr, out var val))
        throw CreateException($"Failed to parse number: {numStr}");

    var unitStart = _pos;
    while (!IsAtEnd() && (char.IsLetter(Peek()) || Peek() == '%')) Advance();
    var unit = dsl[unitStart.._pos];

    return new NumberValue(val, unit);
}
```

Two parts: the **digit run** (with optional leading minus and decimal point) and the **unit suffix**.

Examples:

| Input | `Value` | `Unit` |
|---|---|---|
| `14` | `14` | `""` |
| `14px` | `14` | `"px"` |
| `0.5` | `0.5` | `""` |
| `90deg` | `90` | `"deg"` |
| `100%` | `100` | `"%"` |
| `1s` | `1` | `"s"` |
| `500ms` | `500` | `"ms"` |
| `-3` | `-3` | `""` |
| `2m` | `2` | `"m"` |

The unit is **arbitrary text** — the parser doesn't validate it. Validation happens later, in code that interprets the `NumberValue.Unit`. For example, [[Animations|Animations]] only accepts `ms`, `s`, `m` for `duration`; anything else throws.

The dot is included via `Peek() == '.'` — so `0.5`, `.5` (technically), and `1.0` all work. `1.0.0` would parse as `1.0` followed by a parse error on the leftover `.0`.

## Strings

```csharp
private string ReadString() {
    Consume('"');
    var start = _pos;
    while (!IsAtEnd() && Peek() != '"') Advance();
    if (IsAtEnd()) throw CreateException("Unterminated string");
    var s = dsl[start.._pos];
    Consume('"');
    return s;
}
```

Double-quoted only. **No escape sequences** — there's no way to embed a `"` in a string. For UI text this is fine (the use case is short identifiers like `"linear"`, `"horizontal"`, `"center"`). If you need a literal quote inside a stylesheet value, you can't.

### The hex-color string special case

```csharp
private IStyleValue ParseValue() {
    ...
    if (Check('"')) {
        var s = ReadString();
        if (s.StartsWith('#')) return new ColorValue(s);
        return new StringValue(s);
    }
    ...
}
```

A string that begins with `#` is reinterpreted as a `ColorValue`. So:

```css
background = "#ff0000ff";
```

produces `ColorValue("#ff0000ff")`, **not** `StringValue("#ff0000ff")`. This matters for matching — the [[../Components/Panels#Panel|Panel]]'s `ApplyStyleValue` only treats a value as a background color when it's a `ColorValue` or `GradientValue`.

You can also write:

```css
background = #ff0000ff;
```

(unquoted hex — see below). Both produce `ColorValue` with the same vector.

## Hex colors (unquoted)

```csharp
private string ReadHexColor() {
    var start = _pos;
    Consume('#');
    while (!IsAtEnd() &&
           (char.IsDigit(Peek()) || (char.ToLower(Peek()) >= 'a' && char.ToLower(Peek()) <= 'f'))) Advance();
    return dsl[start.._pos];
}
```

A `#` followed by hex digits (case-insensitive). 6 hex digits = `#RRGGBB` (alpha defaults to 255). 8 hex digits = `#RRGGBBAA`.

```csharp
internal static Vector4 ParseColorFromHex(ReadOnlySpan<char> hex) {
    var hexTrimmed = hex.TrimStart('#');
    if (hexTrimmed.Length is not (6 or 8))
        throw new ArgumentException("Invalid hex color format, expected #RRGGBB(AA)");

    var r = byte.Parse(hexTrimmed[..2], NumberStyles.HexNumber);
    var g = byte.Parse(hexTrimmed[2..4], NumberStyles.HexNumber);
    var b = byte.Parse(hexTrimmed[4..6], NumberStyles.HexNumber);
    byte a = 255;
    if (hexTrimmed.Length == 8)
        a = byte.Parse(hexTrimmed[6..8], NumberStyles.HexNumber);

    return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
}
```

The validation is strict — 3-digit shorthand (`#fff`) is **not supported**. The error message is helpful: "expected #RRGGBB(AA)".

The result is a normalised `Vector4` (R, G, B, A) in `[0, 1]`. This is what [[../Components/Abstractions#UIElement|UIElement]] background-color setters expect.

## Comments

```csharp
private void SkipWhitespaceAndComments() {
    while (!IsAtEnd())
        if (char.IsWhiteSpace(Peek())) Advance();
        else if (Peek() == '/' && PeekNext() == '/')
            while (!IsAtEnd() && Peek() != '\n')
                Advance();
        else break;
}
```

`//` to end-of-line. **No block comments** (`/* ... */`).

Comments are stripped during whitespace skipping, so they can appear anywhere whitespace can. They're not preserved in the AST.

## Whitespace

`char.IsWhiteSpace` — spaces, tabs, newlines, carriage returns, form feeds. Insignificant everywhere outside strings.

The `SkipWhitespaceAndComments` is called liberally throughout the parser — between every token, basically. So:

```css
component button{padding=5px;background="#000000";}
```

and

```css
component
    button
{
    padding    = 5px;
    background = "#000000";
}
```

parse identically.

## Word boundaries — `Match`

```csharp
private bool Match(ReadOnlySpan<char> s) {
    if (_pos + s.Length > dsl.Length) return false;
    if (!dsl.AsSpan(_pos, s.Length).SequenceEqual(s)) return false;

    // Ensure word boundary
    if (_pos + s.Length < dsl.Length &&
        (char.IsLetterOrDigit(dsl[_pos + s.Length]) || dsl[_pos + s.Length] == '-')) return false;

    _pos += s.Length;
    return true;
}
```

Used for matching keyword tokens like `"animation"`, `"component"`, `"class"`, `"id"`, `"import"`, `"state"`. The word-boundary check prevents `"componentX"` from matching as `"component"` followed by a leftover `X`.

The boundary character set is `letter | digit | -` — same as identifier characters.

## Span-based reads

`StyleParser` uses `ReadOnlySpan<char>` for `Match` to avoid allocating a substring per token comparison. Identifiers, strings, and hex colors return `string` (slicing `dsl[start.._pos]`) because they need to be stored in the resulting AST.

## Error reporting — `CreateException`

```csharp
private Exception CreateException(string message) {
    const int linesBefore = 5;
    const int linesAfter = 5;

    var errorPosition = _pos;
    var text = dsl.AsSpan();
    ...
    var slice = text[startI..endI];
    var normalizedPosition = errorPosition - startI;
    var stringified = slice.ToString();

    stringified = stringified.Insert(normalizedPosition, "<--- HERE");
    return new Exception(message + ".\n" +
                         "=== SOURCE CODE===\n\n" +
                         stringified);
}
```

When the parser hits an unexpected token, the exception message includes:

1. The error message itself.
2. **Five lines before and after the error position**, taken straight from the source.
3. A `<--- HERE` marker inserted at the exact cursor offset.

Example output:

```
Expected '{' but found character ';'.
=== SOURCE CODE===

component button ;
                  <--- HERE
    padding = 5px;
    background = "#000000";
}
```

Far better than a bare "syntax error at column 17."

## Position tracking

`_pos` is a single int — character offset into the source. There's no line/column tracking. The error reporter computes context by counting newlines outward from `_pos`, which is fine for showing context but means the error message doesn't include "line 14, column 5"-style coordinates.

This is a deliberate simplification — line/column tracking would require either two extra ints maintained during `Advance()`, or a one-shot scan to convert position to line:col when an error fires. The current approach is good enough; the source-code excerpt is what users actually need.

## What gets called from where

```
ParseSheet
    ├── Match("animation"|"component"|"class"|"id"|"import")
    │   ├── ParseBlock                      ← for animation/component/class/id
    │   │   ├── ReadIdentifier              ← block name
    │   │   ├── Consume('{')
    │   │   ├── (loop)
    │   │   │   ├── ReadIdentifier          ← property name
    │   │   │   ├── Consume('=')
    │   │   │   ├── ParseValue              ← see below
    │   │   │   └── (optional) ';' or ','
    │   │   └── Consume('}')
    │   └── ParseImport                     ← for import
    │       ├── ReadString                  ← path
    │       └── (recursive new StyleParser if fileLoader provided)
    └── '@' prefix → ParseBlock with isOverride=true
```

`ParseValue` dispatches on the first character:

```
ParseValue
    ├── '"'         → ReadString → ColorValue if starts '#', else StringValue
    ├── digit | '-' → ParseNumber → NumberValue
    ├── '#'         → ReadHexColor → ColorValue
    ├── '!'         → ParseKeyword → OverrideValue|GradientValue|KeyframesValue|StopsValue|DirectionValue|KeywordValue
    ├── '{'         → ParseNestedBlock → BlockValue
    ├── '['         → ParseArrayOrMap → ArrayValue or MapValue
    └── identifier  → "vec2"|"vec3"|"vec4" → ParseVector(N) → VectorValue
                      else                 → StringValue (bare-identifier-as-string)
```

The bare-identifier-as-string fallback is what makes `direction = horizontal;` work — `horizontal` is an identifier that becomes a `StringValue("horizontal")`. Quoted form (`"horizontal"`) produces the same result.

## Related

- [[Blocks|Blocks]] — what `ParseBlock` produces.
- [[Style Types|Style Types]] — what `ParseValue` produces.
- [[Animations|Animations]] — the runtime side of `!keyframes`.
- [[Import|Import]] — `ParseImport` and the recursion.
- [[Style DSL|Style DSL]] — the index page.
