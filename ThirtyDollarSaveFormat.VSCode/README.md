# Thirty Dollar Save Format Extension

This extension provides syntax highlighting for the custom Thirty Dollar Save Format, including TDWex events.

## Features

- Highlights sound events, actions, and custom TDWex directives.
- Supports comment blocks and single-line (pipe-delimited) comments.
- Differentiates pitch (`@`), value scale (`@x`, `@+`, `@/`), volume (`%`), panning (`^`),
  sound start offset (`>`), and repetition (`=`).
- Highlights the sounds silenced by an individual cut — both `!cut@kick,🥁` and `#icut(kick, snare)`.
- Understands the packed colour events `!bg@#RRGGBBAA,fade` and `!pulse@count,frequency`.
- Folds `#define(…)` / `#enddefine` blocks, and marks unknown `!actions` as invalid.

## Usage

1. Install this extension.
2. Open any file with the `.moai`,`.🗿`,`.tdw`,`.tdwex` extensions.
3. Enjoy syntax highlighting for your covers and sequences!

## Development

`tests/highlighting.tdwex` plus its `.snap` file pin the scopes the grammar produces.
After changing the grammar run:

```sh
npm install
npm test                 # verify against the snapshot
npm test -- --updateSnapshot   # accept intended changes
```

## Contributing

Feel free to open issues or pull requests for improvements!
