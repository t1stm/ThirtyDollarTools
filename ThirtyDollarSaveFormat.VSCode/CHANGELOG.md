# Changelog

## 0.1.0

- Sound names now match the parser's rule (`^[^@%^=>]*`), so names with dashes, dots or spaces
  highlight in full instead of being cut at the first unusual character.
- Added the sound start offset parameter (`>0.25`).
- `!cut@kick,🥁` and `#icut(kick, snare)` now highlight the sounds they silence.
- `!bg@#RRGGBBAA,fade` and `!pulse@count,frequency` get their own colour/number highlighting.
- Added `!highspeed`; `#bookmark(N)`, `#legacy` and `#enddefine` are now proper keywords.
- Unknown `!actions` are marked invalid, catching typos like `!speeed`.
- Folding for `#define(…)` / `#enddefine` blocks.
- Snapshot tests (`npm test`).

## 0.0.1

- Initial release with basic syntax highlighting for TDW and TDWex events.
