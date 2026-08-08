# Phase 1 — Getting All Samples

> Owning class: `ThirtyDollarConverter/SampleHolder.cs`

Before any sequence can be encoded, the converter needs the actual sound files. The Thirty Dollar Website hosts every "default" sound at `https://thirtydollar.website/sounds/{id}.wav`, listed in a manifest at `https://thirtydollar.website/sounds.json`. Phase 1 is responsible for fetching that manifest, downloading any missing samples to a local cache, and discovering custom user-supplied samples that live alongside them.

This phase is implemented entirely by `SampleHolder`. It is intentionally **disk-and-network heavy and CPU light**; the next phase ([Loading Into Memory](2%20-%20Loading%20Into%20Memory.md)) is what actually decodes those `.wav` files.

## What `SampleHolder` is

```csharp
public class SampleHolder(ILogger logger)
{
    public const string ThirtyDollarWebsiteUrl = "https://thirtydollar.website";
    public const string DownloadSampleUrl     = "https://thirtydollar.website/sounds";

    public Dictionary<Sound, PcmDataHolder> SampleList            { get; } = new();
    public Dictionary<string, Sound>        StringToSoundReferences { get; } = new();
    public Action<Sound, int, int>?         DownloadUpdate { get; set; }

    public string SamplesLocation { get; init; } = $".{Slash}Sounds";
    public string ImagesLocation  => $"{SamplesLocation}{Slash}Images";
}
```

Two dictionaries are the heart of the class:

- `SampleList: Dictionary<Sound, PcmDataHolder>` — for every known sound (TDW + custom), the value is the parsed PCM data once Phase 2 has run. During Phase 1 the values are empty `PcmDataHolder` placeholders.
- `StringToSoundReferences: Dictionary<string, Sound>` — both the sound's `Id` and (if present) its `Emoji` map to the same `Sound`. Encoders and the parser use this to translate a sound event string (`🥁`, `tab`, …) to a canonical `Sound`.

`SamplesLocation` defaults to `./Sounds` and is created on demand by `PrepareDirectory()`.

`Sound` itself is a JSON-deserializable record from [Parsing Sequences](3%20-%20Parsing%20Sequences.md) / `Sound.cs` containing `Id`, optional `Emoji`, `Name`, `Source`, and `UseID` (a flag used when a sound's "name" should always be its ID, not its emoji).

## Step 1: `LoadSampleList()`

```csharp
public async Task LoadSampleList()
{
    if (SampleList.Count > 0) return;
    var sample_list_location = $"{SamplesLocation}{Slash}sounds.json";
    SampleList.Clear();
    PrepareDirectory();

    // Try to fetch sounds.json from thirtydollar.website
    try {
        await using var response = await Client.GetStreamAsync($"{ThirtyDollarWebsiteUrl}/sounds.json");
        await using var fs = File.Open(sample_list_location, FileMode.Create, ...);
        await response.CopyToAsync(fs);
    }
    catch {
        // If offline, fall back to the cached file. If neither exists, throw.
        if (!File.Exists(sample_list_location))
            throw new InvalidProgramException("Cache file 'sounds.json' not found.");
    }

    // Deserialize into Sound[]
    var sounds = await JsonSerializer.DeserializeAsync<Sound[]>(open_file_stream);

    lock (SampleList) {
        foreach (var sound in sounds) {
            StringToSoundReferences.TryAdd(sound.Id, sound);
            if (sound.Emoji != null)
                StringToSoundReferences.TryAdd(sound.Emoji, sound);

            SampleList.TryAdd(sound, new PcmDataHolder());
        }
    }
}
```

Highlights:

- **Network-first, cache-fallback.** It tries to download a fresh manifest, but if the website is unreachable it uses the previously-saved one. If neither is available, it throws `InvalidProgramException` — there's nothing to encode without it.
- **Idempotent.** If `SampleList` is non-empty it returns immediately. Front-ends typically call this once at startup.
- **Empty `PcmDataHolder` placeholders.** At the end of Phase 1 the dictionary keys (sounds) are known but the values still contain no audio bytes — Phase 2 fills them in.

## Step 2: `DownloadSamples(checkOnly = false)`

```csharp
public async Task<bool> DownloadSamples(bool checkOnly = false)
{
    // Probe mode: returns false as soon as one .wav is missing.
    if (checkOnly) {
        foreach (var (sound, _) in SampleList)
            if (!File.Exists($"{SamplesLocation}{Slash}{sound.Id}.wav")) return false;
        return true;
    }

    // Real mode: parallel HTTP downloads, skipping files already on disk.
    await Parallel.ForEachAsync(SampleList, async (pair, token) => {
        var sound = pair.Key;
        var dll   = $"{SamplesLocation}{Slash}{sound.Id}.wav";
        if (File.Exists(dll)) return;

        await using var http = await client.GetStreamAsync(
            $"{DownloadSampleUrl}/{sound.Id}.wav", token);
        await using var fs   = File.Open(dll, FileMode.Create);
        await http.CopyToAsync(fs, token);

        DownloadUpdate?.Invoke(sound, i, count); // progress callback
    });

    return true;
}
```

Notes:

- **Parallel.** `Parallel.ForEachAsync` lets the runtime pick the degree of parallelism; on a fast network this saturates the connection while keeping the per-task cost low.
- **Skip-if-exists.** Files already on disk are not re-downloaded. This is what makes second-runs fast.
- **`checkOnly` mode.** GUI uses this to short-circuit whether to even open its "Downloader" window: if every sound is already cached, it skips straight to encoding.
- **`DownloadUpdate`.** Optional progress callback. The GUI subscribes to update its progress bar; the CLI ignores it.

## Step 3 (optional): `DownloadImages()`

The Visualizer needs little PNG icons for each sound (the emoji thumbnails or the pre-rendered `action_*.png` glyphs for events like `!loop`, `!stop`, `!cut`, …). The `DownloadImages` method is identical in shape to `DownloadSamples` but pulls from two URL templates:

- For sounds with an emoji → Twemoji CDN: `https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72/{codepoint}.png`
- For sounds without an emoji → `https://thirtydollar.website/icons/{id}.png`
- For action events → `https://thirtydollar.website/assets/{action_*}.png`

The list of "action" PNGs is hard-coded:

```csharp
public static readonly string[] ActionsArray =
[
    "action_bg",       "action_combine",  "action_cut",   "action_divider",
    "action_flash",    "action_jump",     "action_loop",  "action_loopmany",
    "action_looptarget","action_pulse",   "action_speed", "action_startpos",
    "action_stop",     "action_target",   "action_transpose","action_volume"
];
```

The Converter itself never *uses* these images, but the same `SampleHolder` is shared with the Visualizer/GUI which do.

## Where Phase 1 hands off

After `LoadSampleList()` + `DownloadSamples()` complete, `SampleHolder` exposes:

- a populated `SampleList` (keys = every TDW + custom sound, values = empty `PcmDataHolder`),
- a populated `StringToSoundReferences` (every alias → canonical Sound),
- `.wav` files on disk for every sound at `{SamplesLocation}/{id}.wav`.

It does **not** have audio in memory yet. That happens in [Loading Into Memory](2%20-%20Loading%20Into%20Memory.md).

## Front-end usage (illustrative)

```csharp
var holder = new SampleHolder(serilogLogger) { SamplesLocation = "./Sounds" };
await holder.LoadSampleList();        // Phase 1a — manifest
await holder.DownloadSamples();       // Phase 1b — download
holder.LoadSamplesIntoMemory();       // Phase 2  — see Loading Into Memory
```

This exact sequence is used in `ThirtyDollarConverter.CLI/Program.cs` and analogously in the GUI's `MainWindowViewModel`. The Discord Bot performs Phase 1 at process startup (in `Static.cs`) and reuses the same holder for every request.

---

**Next:** [Loading Into Memory](2%20-%20Loading%20Into%20Memory.md) — turning the `.wav` files on disk into `PcmDataHolder` / `AudioData<float>` in memory.
**Up:** [Converter](../Converter.md)
