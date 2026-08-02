using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     Saves and loads projects. The DTO records below define the file layout,
///     independent of the domain classes - swap the serializer here when the
///     hand-editable custom format replaces JSON.
/// </summary>
public static class ProjectFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new InstrumentSoundDtoConverter() }
    };

    public static string Save(ThirtyDollarProject project)
    {
        var dto = new ProjectDto(
            project.Info,
            project.RootTiming,
            project.Tracks.Select(track => new TrackDto(
                track.Id,
                track.Name,
                // Shared root timing is omitted and rewired on load; JSON references would
                // otherwise litter the file with $id/$ref noise.
                ReferenceEquals(track.Timing, project.RootTiming) ? null : track.Timing,
                track.Segments.Select(segment => new SegmentDto(
                    segment.Numerator,
                    segment.Denominator,
                    segment.BPM,
                    segment.Bars,
                    segment.StepsPerBeat,
                    segment.Notes.Select(note => new NoteDto(
                        note.Step,
                        note.Instrument.Id,
                        note.Value,
                        note.Volume,
                        note.Pan,
                        SaveAutomation(note.Automation),
                        note.Offset == 0 ? null : note.Offset,
                        IsCut: note.IsCut ? true : null
                    )).ToList()
                )).ToList(),
                track.TrackAutomations.Count == 0
                    ? null
                    : track.TrackAutomations.Select(automation => new TrackAutomationDto(
                        SaveAutomation(automation.Keyframes)!,
                        automation.Sounds)).ToList(),
                track.Transpose
            )).ToList(),
            project.Instruments.Select(instrument => new InstrumentDto(
                instrument.Id,
                instrument.Name,
                instrument.Sounds.Select(sound => new InstrumentSoundDto(
                    sound.Sound, sound.Value, sound.Volume, sound.Pan)).ToList())).ToList(),
            project.Placements.Select(placement => new PlacementDto(
                placement.Track.Id,
                placement.Channel,
                placement.StartQuarterNotes)).ToList(),
            project.Transpose == 0 ? null : project.Transpose);

        return JsonSerializer.Serialize(dto, Options);
    }

    public static ThirtyDollarProject Load(string json)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(json, Options)
                  ?? throw new InvalidDataException("Not a Thirty Dollar project file.");

        var project = new ThirtyDollarProject
        {
            Info = dto.Info,
            RootTiming = dto.RootTiming,
            Transpose = dto.Transpose ?? 0
        };

        var instruments_by_id = new Dictionary<int, Instrument>();
        foreach (var instrument_dto in dto.Instruments ?? [])
        {
            var instrument = project.AddInstrument(instrument_dto.Id, instrument_dto.Name);
            foreach (var sound_dto in instrument_dto.Sounds)
            {
                // Legacy files wrote sounds as bare names plus an adjustment map keyed by
                // name (one adjustment per sound, no duplicates); merge that in here.
                var legacy = instrument_dto.Adjustments?.GetValueOrDefault(sound_dto.Sound);
                instrument.Sounds.Add(new InstrumentSound
                {
                    Sound = sound_dto.Sound,
                    Value = legacy?.Value ?? sound_dto.Value,
                    Volume = legacy?.Volume ?? sound_dto.Volume,
                    Pan = legacy?.Pan ?? sound_dto.Pan
                });
            }

            instruments_by_id[instrument.Id] = instrument;
        }

        foreach (var track_dto in dto.Tracks ?? [])
        {
            var track = project.AddTrack(track_dto.Id, track_dto.Timing);
            track.Name = track_dto.Name;
            track.Transpose = track_dto.Transpose;

            var segments = track_dto.Segments ?? [];
            for (var i = 0; i < segments.Count; i++)
            {
                var segment_dto = segments[i];
                var segment = i == 0 ? track.Segments[0] : track.NewSegment();
                segment.Numerator = segment_dto.Numerator;
                segment.Denominator = segment_dto.Denominator;
                segment.BPM = segment_dto.BPM;
                segment.Bars = segment_dto.Bars;
                segment.StepsPerBeat = segment_dto.StepsPerBeat;

                foreach (var note in segment_dto.Notes ?? [])
                {
                    // Migration: pre-instrument files carried a bare sound name per note;
                    // dedup those into one instrument per sound (see GetOrCreateInstrument).
                    var instrument = note.InstrumentId is { } instrument_id
                        ? instruments_by_id[instrument_id]
                        : project.GetOrCreateInstrument(note.Sound!);

                    segment.Notes.Add(new Note
                    {
                        Step = note.Step,
                        Instrument = instrument,
                        Value = note.Value,
                        Volume = note.Volume,
                        Pan = note.Pan,
                        Offset = note.Offset ?? 0,
                        Automation = LoadAutomation(note.Automation),
                        IsCut = note.IsCut ?? false
                    });
                }
            }

            foreach (var automation_dto in track_dto.TrackAutomations ?? [])
                track.AddTrackAutomation(LoadAutomation(automation_dto.Automation)!, automation_dto.Sounds);
        }

        if (dto.Placements is null)
        {
            // Pre-arrangement files played every track from time 0; materialize that
            // as real placements. An explicitly empty list stays empty.
            var channel = 0;
            foreach (var track in project.Tracks) project.Place(track, channel++, 0);
        }
        else
        {
            var tracks_by_id = project.Tracks.ToDictionary(track => track.Id);
            foreach (var placement in dto.Placements)
                if (tracks_by_id.TryGetValue(placement.TrackId, out var track))
                    project.Place(track, placement.Channel, placement.Start);
        }

        return project;
    }

    private static AutomationDto? SaveAutomation(AudioKeyframeManager? manager)
    {
        return manager is null
            ? null
            : new AutomationDto(
                manager.Timing,
                manager.Keyframes.Select(keyframe => new KeyframeDto(
                    keyframe.Gap,
                    NullIfNoOp(keyframe.Value),
                    NullIfNoOp(keyframe.Volume),
                    NullIfNoOp(keyframe.Pan),
                    NullIfNoOp(keyframe.Offset),
                    keyframe.Cut ? true : null,
                    keyframe.CutOnly ? true : null,
                    keyframe.CutLast ? true : null)).ToList(),
                manager.Repeats == 1 ? null : manager.Repeats);
    }

    private static AudioKeyframeManager? LoadAutomation(AutomationDto? dto)
    {
        if (dto is null) return null;

        var manager = new AudioKeyframeManager { Timing = dto.Timing, Repeats = dto.Repeats ?? 1 };
        foreach (var keyframe in dto.Keyframes ?? [])
            manager.Keyframes.Add(new AudioKeyframe
            {
                Gap = keyframe.Gap,
                Value = keyframe.Value ?? default,
                Volume = keyframe.Volume ?? default,
                Pan = keyframe.Pan ?? default,
                Offset = keyframe.Offset ?? default,
                Cut = keyframe.Cut ?? false,
                CutOnly = keyframe.CutOnly ?? false,
                CutLast = keyframe.CutLast ?? false
            });

        return manager;
    }

    private static Modifier? NullIfNoOp(Modifier modifier)
    {
        return modifier == default ? null : modifier;
    }

    private record ProjectDto(
        ProjectInfo Info,
        TimingInfo RootTiming,
        List<TrackDto> Tracks,
        // Null (missing key) marks a pre-instrument file - its notes carry a bare Sound
        // instead of an InstrumentId, and Load migrates them (see Load).
        List<InstrumentDto>? Instruments = null,
        // Null (missing key) marks a pre-arrangement file - see Load.
        List<PlacementDto>? Placements = null,
        // Null (missing key) = 0 - files from before the feature stay valid.
        float? Transpose = null);

    private record InstrumentDto(int Id, string Name, List<InstrumentSoundDto> Sounds,
        // Legacy (pre-duplicate-sounds) adjustment map keyed by sound name, kept only for
        // reading old files; never written. See Load.
        Dictionary<string, SoundAdjustmentDto>? Adjustments = null);

    /// <summary>One entry of an instrument's "sounds". Older files wrote a bare sound name
    /// here instead of an object - <see cref="InstrumentSoundDtoConverter" /> reads both.</summary>
    private record InstrumentSoundDto(string Sound, double Value = 0, double? Volume = null, float Pan = 0);

    private record SoundAdjustmentDto(double Value, double? Volume, float Pan);

    /// <summary>Reads a bare JSON string as a plain, unadjusted sound, so instrument lists
    /// written before sounds became objects still load. Always writes the object form.</summary>
    private sealed class InstrumentSoundDtoConverter : JsonConverter<InstrumentSoundDto>
    {
        public override InstrumentSoundDto Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return new InstrumentSoundDto(reader.GetString()!);

            // Without the converter stripped from the options, this recurses into itself.
            var plain = new JsonSerializerOptions(options);
            plain.Converters.Remove(plain.Converters.First(c => c is InstrumentSoundDtoConverter));
            return JsonSerializer.Deserialize<InstrumentSoundDto>(ref reader, plain)!;
        }

        public override void Write(Utf8JsonWriter writer, InstrumentSoundDto value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("sound", value.Sound);
            if (value.Value != 0) writer.WriteNumber("value", value.Value);
            if (value.Volume is { } volume) writer.WriteNumber("volume", volume);
            if (value.Pan != 0) writer.WriteNumber("pan", value.Pan);
            writer.WriteEndObject();
        }
    }

    private record PlacementDto(int TrackId, int Channel, double Start);

    private record TrackDto(
        int Id,
        string Name,
        TimingInfo? Timing,
        List<SegmentDto> Segments,
        // Null (missing key) = no track-wide automation.
        List<TrackAutomationDto>? TrackAutomations = null,
        // Null (missing key or explicit) = inherits the project-wide transpose.
        float? Transpose = null);

    private record TrackAutomationDto(AutomationDto Automation, List<string>? Sounds);

    private record SegmentDto(
        int Numerator,
        int Denominator,
        float? BPM,
        int Bars,
        int StepsPerBeat,
        List<NoteDto> Notes);

    private record NoteDto(
        int Step,
        // Null (missing key) marks a pre-instrument note - Load resolves via Sound instead.
        int? InstrumentId,
        double Value,
        double? Volume,
        float Pan,
        AutomationDto? Automation,
        // Sound-start offset in seconds; null (missing key) = 0.
        double? Offset = null,
        // Pre-instrument sound name, kept only for reading old files; never written.
        string? Sound = null,
        // Null (missing key) = false - files from before the feature stay valid.
        bool? IsCut = null);

    // Null Repeats (missing key) = 1 - files from before the feature stay valid.
    private record AutomationDto(KeyframeTiming Timing, List<KeyframeDto> Keyframes, int? Repeats = null);

    private record KeyframeDto(float Gap, Modifier? Value, Modifier? Volume, Modifier? Pan,
        // Null (missing key) = false - files from before the feature stay valid.
        Modifier? Offset = null, bool? Cut = null, bool? CutOnly = null, bool? CutLast = null);
}