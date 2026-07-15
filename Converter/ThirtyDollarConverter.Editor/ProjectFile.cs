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
                        note.Automation is null
                            ? null
                            : new AutomationDto(
                                note.Automation.Timing,
                                note.Automation.Keyframes.Select(keyframe => new KeyframeDto(
                                    keyframe.Gap,
                                    NullIfNoOp(keyframe.Value),
                                    NullIfNoOp(keyframe.Volume),
                                    NullIfNoOp(keyframe.Pan))).ToList())
                    )).ToList()
                )).ToList()
            )).ToList());

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
                        Automation = LoadAutomation(note.Automation)
                    });
            }
        }

        return project;
    }

    private static AudioKeyframeManager? LoadAutomation(AutomationDto? dto)
    {
        if (dto is null) return null;

        var manager = new AudioKeyframeManager { Timing = dto.Timing };
        foreach (var keyframe in dto.Keyframes ?? [])
            manager.Keyframes.Add(new AudioKeyframe
            {
                Gap = keyframe.Gap,
                Value = keyframe.Value ?? default,
                Volume = keyframe.Volume ?? default,
                Pan = keyframe.Pan ?? default
            });

        return manager;
    }

    private static Modifier? NullIfNoOp(Modifier modifier)
    {
        return modifier == default ? null : modifier;
    }

    private record ProjectDto(ProjectInfo Info, TimingInfo RootTiming, List<TrackDto> Tracks);

    private record TrackDto(int Id, string Name, TimingInfo? Timing, List<SegmentDto> Segments);

    private record SegmentDto(int Numerator, int Denominator, float? BPM, int Bars, int StepsPerBeat,
        List<NoteDto> Notes);

    private record NoteDto(int Step, string Sound, double Value, double? Volume, float Pan,
        AutomationDto? Automation);

    private record AutomationDto(KeyframeTiming Timing, List<KeyframeDto> Keyframes);

    private record KeyframeDto(float Gap, Modifier? Value, Modifier? Volume, Modifier? Pan);
}
