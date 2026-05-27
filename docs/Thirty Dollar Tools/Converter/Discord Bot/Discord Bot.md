# Discord Bot

> Owning project: `Converter/ThirtyDollarConverter.DiscordBot/`

The same converter as the [[../Converter.GUI/Converter.GUI|GUI]] and the [[../Converter.CLI/Converter.CLI|CLI]], but exposed as a Discord bot. Users upload (or right-click) a sequence file, the bot encodes it through the same five phases ([[../Phases/1 - Getting All Samples|Getting All Samples]] → [[../Phases/2 - Loading Into Memory|Loading Into Memory]] → [[../Phases/3 - Parsing Sequences|Parsing Sequences]] → [[../Phases/4 - Calculating the Placement|Calculating the Placement]] → [[../Phases/5 - Encoding|Encoding]]), and replies with the audio attachment.

## Files

| File | Purpose |
| --- | --- |
| `Program.cs` | Top-level. Reads `DISCORD_TOKEN` and `SAMPLES_LOCATION` env vars, runs Phases 1–2 once at startup, then hands the `SampleHolder` to `Static`. |
| `Static.cs` | Process-wide singleton holding the shared `SampleHolder`. The `.wav` cache is loaded once and reused for every request. |
| `SlashCommands.cs` | DSharpPlus slash-command + context-menu handlers. Drives a `PcmEncoder`, then pipes the resulting WAV through `ffmpeg` to either `libopus` (192 kbps `.ogg`) or `libmp3lame` (`.mp3`) before uploading. |

## Flow

1. **Startup (Phases 1–2 once):** `LoadSampleList` → `DownloadSamples` → `LoadSamplesIntoMemory`. After this the bot can answer requests without re-touching disk.
2. **Per request:** the user invokes a slash command or "Convert this file" context menu. The bot pulls the sequence, parses it (Phase 3), calculates placements (Phase 4), encodes (Phase 5).
3. **Transcode:** raw 16-bit WAV is piped through ffmpeg to a smaller container before upload (Discord file-size limits make a raw WAV impractical).
4. **Upload:** the result is attached to the reply.

## Limits

- Hard cap of ~15 minutes of output audio (rejected before encoding starts).
- Requires `ffmpeg` on `PATH`.
- The bot uses the same one-shot encode path as the CLI — no incremental render.

## Relationship to the rest of the converter

Same engine as the [[../Converter.GUI/Converter.GUI|GUI]] and [[../Converter.CLI/Converter.CLI|CLI]]. The bot's only domain-specific code is Discord plumbing and the ffmpeg transcode — everything below `PcmEncoder` is identical. See [[../Converter|Converter]] for the engine itself.

---

**Up:** [[../Converter|Converter]]
