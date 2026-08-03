# ThirtyDollarConverter

Core library for parsing and encoding Thirty Dollar Website (TDW) sequences: placement
calculation (`PlacementCalculator`) and PCM/WAV rendering (`PCMEncoder`).

## Known divergences from TDW's parser

`PlacementCalculator` mirrors the reference parser (`preloadSequence` in the TDW client,
used for both live playback and WAV export). The following differences are known and
currently intentional — not yet ported:

- **No BPM clamp after `!speed`.** TDW clamps to `[5, 20000]` after every `!speed` op.
- **No volume upper clamp after `!volume`.** TDW clamps to `[0, 600]`; this converter only
  floors at `0`.
- **No transpose clamp after `!transpose`.** TDW clamps to `[-60, 60]`.
- **No per-note pitch clamp.** TDW clamps the final per-sound pitch (`pitch + transpose`)
  to `[-72, 72]` at schedule time.
- **No per-note volume clamp.** TDW clamps the individual event volume to `[0, 4]`
  (i.e. 0-400%) before multiplying by the global volume.

If a sequence relies on out-of-range values wrapping/saturating the way TDW does, output
will differ until these clamps are added to `PlacementCalculator`.
