namespace ThirtyDollarConverter.Editor.Tests;

public class ProjectFileTests
{
    private static ThirtyDollarProject MakeProject()
    {
        var project = new ThirtyDollarProject();
        project.Info.Name = "Test Song";
        project.Info.Author = "Kris";
        project.RootTiming.BPM = 140;

        var drums = project.NewTrack(); // shares RootTiming
        drums.Name = "Drums";
        var odd = drums.Segments[0];
        odd.Numerator = 7;
        odd.Denominator = 8;
        odd.StepsPerBeat = 2;
        odd.Notes.Add(new Note { Step = 3, Sound = "kick", Value = -5, Volume = 80, Pan = -25 });

        var slow = drums.NewSegment();
        slow.BPM = 60;
        slow.Bars = 2;

        var kickEcho = new AudioKeyframeManager();
        kickEcho.Keyframes.Add(new AudioKeyframe { Gap = 2, Value = new Modifier(3) });
        drums.AddTrackAutomation(kickEcho, ["kick"]);

        var melody = project.NewTrack();
        melody.Name = "Melody";
        melody.Timing = new TimingInfo { BPM = 90 }; // own tempo

        var echo = new AudioKeyframeManager { Timing = KeyframeTiming.Time };
        echo.Keyframes.Add(new AudioKeyframe
        {
            Gap = 0.25f,
            Volume = new Modifier(0.5, ModifierKind.Multiply),
            Value = new Modifier(12)
        });
        melody.Segments[0].Notes.Add(new Note { Step = 0, Sound = "harp", Automation = echo });

        return project;
    }

    [Fact]
    public void RoundTrip_PreservesTheWholeProject()
    {
        var json = ProjectFile.Save(MakeProject());
        var loaded = ProjectFile.Load(json);

        // Canonical form survives a full save -> load -> save cycle unchanged.
        Assert.Equal(json, ProjectFile.Save(loaded));

        Assert.Equal("Test Song", loaded.Info.Name);
        Assert.Equal(140, loaded.RootTiming.BPM);

        var drums = loaded.Tracks[0];
        Assert.Equal("Drums", drums.Name);
        Assert.Equal(7, drums.Segments[0].Numerator);
        Assert.Equal(60, drums.Segments[1].BPM);
        Assert.Equal(2, drums.Segments[1].Bars);

        var kick = drums.Segments[0].Notes[0];
        Assert.Equal(-5, kick.Value);
        Assert.Equal(80, kick.Volume);
        Assert.Equal(-25, kick.Pan);

        var trackAutomation = Assert.Single(drums.TrackAutomations);
        Assert.Equal(["kick"], trackAutomation.Sounds);
        Assert.Equal(new Modifier(3), trackAutomation.Keyframes.Keyframes[0].Value);

        var harp = loaded.Tracks[1].Segments[0].Notes[0];
        Assert.NotNull(harp.Automation);
        Assert.Equal(KeyframeTiming.Time, harp.Automation.Timing);
        var keyframe = Assert.Single(harp.Automation.Keyframes);
        Assert.Equal(new Modifier(0.5, ModifierKind.Multiply), keyframe.Volume);
        Assert.Equal(new Modifier(12), keyframe.Value);
    }

    [Fact]
    public void RootTimingSharing_SurvivesTheRoundTrip()
    {
        var loaded = ProjectFile.Load(ProjectFile.Save(MakeProject()));

        // Drums shared the root timing; changing the project tempo must still reach it.
        Assert.Same(loaded.RootTiming, loaded.Tracks[0].Timing);

        // Melody had its own tempo and must keep it independent.
        Assert.NotSame(loaded.RootTiming, loaded.Tracks[1].Timing);
        Assert.Equal(90, loaded.Tracks[1].Timing.BPM);
    }

    [Fact]
    public void LoadedProject_KeepsTrackIdsUnique()
    {
        var loaded = ProjectFile.Load(ProjectFile.Save(MakeProject()));

        Assert.Equal([1, 2], loaded.Tracks.Select(t => t.Id));
        Assert.Equal(3, loaded.NewTrack().Id);
    }

    [Fact]
    public void SavedJson_IsHumanReadable()
    {
        var json = ProjectFile.Save(MakeProject());

        Assert.Contains("\n  ", json); // indented, not minified
        Assert.Contains("\"multiply\"", json); // enums as words, not magic numbers
        Assert.DoesNotContain("$id", json); // no reference-tracking noise
        Assert.DoesNotContain(": null", json); // absent fields are omitted, not spelled out
    }

    [Fact]
    public void Placements_SurviveTheRoundTrip_AndShareTheirPattern()
    {
        var project = MakeProject();
        var pattern = project.Tracks[0];
        project.Place(pattern, 0, 0);
        project.Place(pattern, 3, 16.5);
        project.Place(project.Tracks[1], 1, 4);

        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        Assert.Equal(3, loaded.Placements.Count);
        Assert.Equal(3, loaded.Placements[1].Channel);
        Assert.Equal(16.5, loaded.Placements[1].StartQuarterNotes);
        // Two clips of the same pattern reference the same loaded track instance —
        // editing the pattern must update every clip.
        Assert.Same(loaded.Placements[0].Track, loaded.Placements[1].Track);
        Assert.Same(loaded.Tracks[0], loaded.Placements[0].Track);
        Assert.Same(loaded.Tracks[1], loaded.Placements[2].Track);
    }

    [Fact]
    public void LegacyFile_WithoutPlacements_AutoPlacesEveryTrackAtZero()
    {
        // Files from before the arrangement layer played all tracks from time 0.
        const string legacy = """
                              {
                                "info": { "name": "Old" },
                                "rootTiming": { "bpm": 120, "numerator": 4, "denominator": 4 },
                                "tracks": [
                                  { "id": 1, "name": "A", "segments": [] },
                                  { "id": 2, "name": "B", "segments": [] }
                                ]
                              }
                              """;

        var loaded = ProjectFile.Load(legacy);

        Assert.Equal(2, loaded.Placements.Count);
        Assert.All(loaded.Placements, p => Assert.Equal(0, p.StartQuarterNotes));
        Assert.Equal([0, 1], loaded.Placements.Select(p => p.Channel));
        Assert.Same(loaded.Tracks[0], loaded.Placements[0].Track);

        // An explicitly empty arrangement stays empty — only a missing key is legacy.
        var project = new ThirtyDollarProject();
        project.NewTrack();
        Assert.Empty(ProjectFile.Load(ProjectFile.Save(project)).Placements);
    }

    [Fact]
    public void TrackAutomation_WithNullSounds_RoundTripsAsAllSounds()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.AddTrackAutomation(new AudioKeyframeManager());

        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        var automation = Assert.Single(loaded.Tracks[0].TrackAutomations);
        Assert.Null(automation.Sounds);
    }

    [Fact]
    public void LegacyFile_WithoutTrackAutomations_LoadsWithNone()
    {
        // Files from before this feature have no "trackAutomations" key at all.
        const string legacy = """
                              {
                                "info": { "name": "Old" },
                                "rootTiming": { "bpm": 120, "numerator": 4, "denominator": 4 },
                                "tracks": [
                                  { "id": 1, "name": "A", "segments": [] }
                                ]
                              }
                              """;

        var loaded = ProjectFile.Load(legacy);

        Assert.Empty(loaded.Tracks[0].TrackAutomations);
    }
}