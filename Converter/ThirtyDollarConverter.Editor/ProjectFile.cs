using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     Saves and loads projects. The DTO records below define the file layout,
///     independent of the domain classes — swap the serializer here when the
///     hand-editable custom format replaces JSON.
/// </summary>
public static class ProjectFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
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
                        note.Sound,
                        note.Value,
                        note.Volume,
                        note.Pan,
                        SaveAutomation(note.Automation),
                        note.Offset == 0 ? null : note.Offset
                    )).ToList()
                )).ToList(),
                track.TrackAutomations.Count == 0
                    ? null
                    : track.TrackAutomations.Select(automation => new TrackAutomationDto(
                        SaveAutomation(automation.Keyframes)!,
                        automation.Sounds)).ToList()
            )).ToList(),
            project.Placements.Select(placement => new PlacementDto(
                placement.Track.Id,
                placement.Channel,
                placement.StartQuarterNotes)).ToList());

        return JsonSerializer.Serialize(dto, Options);
    }

    public static ThirtyDollarProject Load(string json)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(json, Options)
                  ?? throw new InvalidDataException("Not a Thirty Dollar project file.");

        var project = new ThirtyDollarProject
        {
            Info = dto.Info,
            RootTiming = dto.RootTiming
        };

        foreach (var track_dto in dto.Tracks ?? [])
        {
            var track = project.AddTrack(track_dto.Id, track_dto.Timing);
            track.Name = track_dto.Name;

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
                    segment.Notes.Add(new Note
                    {
                        Step = note.Step,
                        Sound = note.Sound,
                        Value = note.Value,
                        Volume = note.Volume,
                        Pan = note.Pan,
                        Offset = note.Offset ?? 0,
                        Automation = LoadAutomation(note.Automation)
                    });
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
                    NullIfNoOp(keyframe.Offset))).ToList(),
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
                Offset = keyframe.Offset ?? default
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
        // Null (missing key) marks a pre-arrangement file — see Load.
        List<PlacementDto>? Placements = null);

    private record PlacementDto(int TrackId, int Channel, double Start);

    private record TrackDto(
        int Id,
        string Name,
        TimingInfo? Timing,
        List<SegmentDto> Segments,
        // Null (missing key) = no track-wide automation.
        List<TrackAutomationDto>? TrackAutomations = null);

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
        string Sound,
        double Value,
        double? Volume,
        float Pan,
        AutomationDto? Automation,
        // Sound-start offset in seconds; null (missing key) = 0.
        double? Offset = null);

    // Null Repeats (missing key) = 1 — files from before the feature stay valid.
    private record AutomationDto(KeyframeTiming Timing, List<KeyframeDto> Keyframes, int? Repeats = null);

    private record KeyframeDto(float Gap, Modifier? Value, Modifier? Volume, Modifier? Pan,
        Modifier? Offset = null);
}