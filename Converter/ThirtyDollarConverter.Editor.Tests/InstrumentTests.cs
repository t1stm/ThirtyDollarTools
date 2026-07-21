namespace ThirtyDollarConverter.Editor.Tests;

public class InstrumentTests
{
    [Fact]
    public void TwoSoundInstrument_RoundTrips_WithNameAndSoundsIntact()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.Sounds.Add("kick");
        layer.Sounds.Add("clap");

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer });

        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        var instrument = Assert.Single(loaded.Instruments);
        Assert.Equal("Layer", instrument.Name);
        Assert.Equal(["kick", "clap"], instrument.Sounds);
        Assert.Same(instrument, loaded.Tracks[0].Segments[0].Notes[0].Instrument);
    }

    [Fact]
    public void LegacyFile_NotesSharingAnOldSound_MigrateToOneDedupedInstrument()
    {
        // Pre-instrument shape: notes carry a bare "sound", no "instruments"/"instrumentId".
        const string legacy = """
                              {
                                "info": { "name": "Old" },
                                "rootTiming": { "bpm": 120, "numerator": 4, "denominator": 4 },
                                "tracks": [
                                  {
                                    "id": 1,
                                    "name": "Drums",
                                    "segments": [
                                      {
                                        "numerator": 4, "denominator": 4, "bars": 1, "stepsPerBeat": 4,
                                        "notes": [
                                          { "step": 0, "sound": "kick", "value": 0, "pan": 0 },
                                          { "step": 4, "sound": "kick", "value": 0, "pan": 0 },
                                          { "step": 8, "sound": "snare", "value": 0, "pan": 0 }
                                        ]
                                      }
                                    ]
                                  }
                                ]
                              }
                              """;

        var loaded = ProjectFile.Load(legacy);

        Assert.Equal(2, loaded.Instruments.Count);
        var notes = loaded.Tracks[0].Segments[0].Notes;
        Assert.Same(notes[0].Instrument, notes[1].Instrument); // two "kick" notes share one instrument
        Assert.NotSame(notes[0].Instrument, notes[2].Instrument);
        Assert.Equal("kick", notes[0].Instrument.Name);
        Assert.Equal(["kick"], notes[0].Instrument.Sounds);
        Assert.Equal("snare", notes[2].Instrument.Name);

        // A pre-instrument file must render identically to a hand-built single-sound one.
        var handBuilt = new ThirtyDollarProject { RootTiming = { BPM = 120 } };
        var track = handBuilt.NewTrack();
        var kick = handBuilt.NewInstrument("kick");
        kick.Sounds.Add("kick");
        var snare = handBuilt.NewInstrument("snare");
        snare.Sounds.Add("snare");
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = kick });
        track.Segments[0].Notes.Add(new Note { Step = 4, Instrument = kick });
        track.Segments[0].Notes.Add(new Note { Step = 8, Instrument = snare });
        handBuilt.Place(track, 0, 0);

        Assert.Equal(handBuilt.ToSequence().Events.Select(e => e.SoundEvent),
            loaded.ToSequence().Events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void SoundAdjustment_ValuesAddToTheNotesValue_WhenTheNotePlays()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.Sounds.Add("kick");
        layer.Sounds.Add("snare");
        layer.Adjustments["kick"] = new SoundAdjustment { Value = -3 };
        layer.Adjustments["snare"] = new SoundAdjustment { Value = -9 };

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer, Value = 3 });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;
        Assert.Equal(0, events.Single(e => e.SoundEvent == "kick").Value);
        Assert.Equal(-6, events.Single(e => e.SoundEvent == "snare").Value);
    }

    [Fact]
    public void SoundAdjustment_VolumeOverridesTheNotesVolume_InsteadOfStackingOnTopOfIt()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.Sounds.Add("kick");
        layer.Sounds.Add("snare");
        // "kick" has its own fixed volume; "snare" is untouched and should just follow the note.
        layer.Adjustments["kick"] = new SoundAdjustment { Volume = 105 };

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer, Volume = 50 });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;
        Assert.Equal(105, events.Single(e => e.SoundEvent == "kick").Volume);
        Assert.Equal(50, events.Single(e => e.SoundEvent == "snare").Volume);
    }

    [Fact]
    public void SoundAdjustment_RoundTrips_ThroughProjectFile()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.Sounds.Add("kick");
        layer.Adjustments["kick"] = new SoundAdjustment { Value = -3, Volume = 20, Pan = -15 };

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer });

        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        var adjustment = Assert.Single(loaded.Instruments).Adjustments["kick"];
        Assert.Equal(-3, adjustment.Value);
        Assert.Equal(20, adjustment.Volume);
        Assert.Equal(-15, adjustment.Pan);
    }
}
