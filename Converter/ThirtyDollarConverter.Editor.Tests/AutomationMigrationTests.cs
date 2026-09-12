namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     Sustains used to be hand-built out of per-keyframe gaps and "repeats". Those files
///     have to open as long notes with the same timing they always had - the conversion
///     happens once, on load, so nothing below the file layer ever sees the old shape.
/// </summary>
public class AutomationMigrationTests
{
    private static string LegacyProject(string automation)
    {
        return $$"""
                 {
                   "info": { "name": "Legacy" },
                   "rootTiming": { "bpm": 120, "numerator": 4, "denominator": 4 },
                   "tracks": [
                     {
                       "id": 1,
                       "name": "Track 1",
                       "segments": [
                         {
                           "numerator": 4, "denominator": 4, "bars": 4, "stepsPerBeat": 4,
                           "notes": [
                             { "step": 0, "instrumentId": 1, "value": 0, "pan": 0, "automation": {{automation}} }
                           ]
                         }
                       ]
                     }
                   ],
                   "instruments": [ { "id": 1, "name": "loop", "sounds": [ { "sound": "loop" } ] } ],
                   "placements": [ { "trackId": 1, "channel": 0, "start": 0 } ]
                 }
                 """;
    }

    private static AudioKeyframeManager Load(string automation)
    {
        return ProjectFile.Load(LegacyProject(automation)).Tracks[0].Segments[0].Notes[0].Automation!;
    }

    private static float[] Positions(AudioKeyframeManager manager)
    {
        return [.. Enumerable.Range(0, manager.Keyframes.Count).Select(manager.PositionOf)];
    }

    [Fact]
    public void EvenSustain_BecomesALongNoteOnTheSameGrid()
    {
        // One cutting keyframe a step wide, run three times - the old way to sustain 4 steps.
        var manager = Load("""
                           {
                             "timing": "step",
                             "repeats": 3,
                             "keyframes": [ { "gap": 1, "cut": true, "cutLast": true } ]
                           }
                           """);

        Assert.Equal(1, manager.Gap);
        Assert.Equal(4, manager.End);
        Assert.True(manager.Cut);
        Assert.True(manager.CutAtEnd);
        Assert.Equal([1f, 2f, 3f], Positions(manager));
        // An even sustain converts to a clean automation: nothing is pinned off the grid.
        Assert.All(manager.Keyframes, keyframe => Assert.Null(keyframe.Position));
    }

    [Fact]
    public void UnevenGaps_SurviveAsPerKeyframePositions()
    {
        // 1 step, then 3, twice over: a swung retrigger the derived grid cannot express.
        var manager = Load("""
                           {
                             "timing": "step",
                             "repeats": 2,
                             "keyframes": [
                               { "gap": 1, "cut": true, "value": { "amount": 2, "kind": "add" } },
                               { "gap": 3, "cut": true }
                             ]
                           }
                           """);

        Assert.Equal([1f, 4f, 5f, 8f], Positions(manager));
        Assert.Equal(9, manager.End);
        Assert.True(manager.Cut);
        Assert.False(manager.CutAtEnd);
        // The per-keyframe values repeat across the old cycles, in place.
        Assert.Equal([new Modifier(2), default, new Modifier(2), default],
            manager.Keyframes.Select(keyframe => keyframe.Value));
    }

    [Fact]
    public void CutOnlyKeyframes_BecomeSilentOnes_KeepingTheCutAndTheTiming()
    {
        var manager = Load("""
                           {
                             "timing": "step",
                             "repeats": 2,
                             "keyframes": [ { "gap": 4, "cut": true, "cutOnly": true, "cutLast": true } ]
                           }
                           """);

        Assert.Equal([4f, 8f], Positions(manager));
        Assert.Equal(12, manager.End);
        Assert.True(manager.CutAtEnd);
        Assert.All(manager.Keyframes,
            keyframe => Assert.Equal(new Modifier(0, ModifierKind.Multiply), keyframe.Volume));

        // The generated notes are placed but silent, so the cuts land where they used to.
        var project = ProjectFile.Load(LegacyProject("""
                                                    {
                                                      "timing": "step",
                                                      "repeats": 2,
                                                      "keyframes": [ { "gap": 4, "cut": true, "cutOnly": true, "cutLast": true } ]
                                                    }
                                                    """));
        var events = project.ToSequence().Events;
        Assert.Equal([0d, 0d], events.Where(e => e.SoundEvent == "loop").Skip(1).Select(e => e.Volume));
    }

    [Fact]
    public void ConvertedAutomation_RoundTripsInTheNewShape()
    {
        var legacy = LegacyProject("""
                                   {
                                     "timing": "step",
                                     "repeats": 2,
                                     "keyframes": [
                                       { "gap": 1, "cut": true, "volume": { "amount": 0.5, "kind": "multiply" } },
                                       { "gap": 3, "cut": true }
                                     ]
                                   }
                                   """);

        var converted = ProjectFile.Save(ProjectFile.Load(legacy));
        Assert.DoesNotContain("repeats", converted);

        // Converted once, stable from then on.
        Assert.Equal(converted, ProjectFile.Save(ProjectFile.Load(converted)));
        Assert.True(ProjectFile.Load(legacy).Tracks[0].Segments[0].Notes[0].Automation!
            .ValueEquals(ProjectFile.Load(converted).Tracks[0].Segments[0].Notes[0].Automation!));
    }
}
