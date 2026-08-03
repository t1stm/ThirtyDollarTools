# ThirtyDollarConverter

Core library for parsing and encoding Thirty Dollar Website (TDW) sequences: placement
calculation (`PlacementCalculator`) and PCM/WAV rendering (`PCMEncoder`).

## Known divergences from TDW's parser

`PlacementCalculator` mirrors the reference parser (`preloadSequence` in the TDW client,
used for both live playback and WAV export). TDW clamps several running values that this
converter leaves unclamped by default. Each clamp can be turned on individually via
`EncoderSettings`, off by default so existing output doesn't change:

| `EncoderSettings` flag | TDW behavior when enabled |
| --- | --- |
| `ClampBpm` | Clamps BPM to `[5, 20000]` after every `!speed` op. |
| `ClampVolume` | Clamps the global volume to `[0, 600]` after every `!volume` op (the volume is always floored at `0` regardless of this flag). |
| `ClampTranspose` | Clamps the running transpose value to `[-60, 60]` after every `!transpose` op. |
| `ClampPitch` | Clamps each note's final pitch (its own value plus the running transpose) to `[-72, 72]`. |
| `ClampNoteVolume` | Clamps each note's own volume ratio to `[0, 4]` (0-400%) before it's multiplied by the global volume. |

If a sequence relies on out-of-range values wrapping/saturating the way TDW does, enable
the relevant flag(s) above.
