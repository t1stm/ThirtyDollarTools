using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class InstrumentTests
{
    [Fact]
    public void TwoSoundInstrument_RoundTrips_WithNameAndSoundsIntact()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.AddSound("kick");
        layer.AddSound("clap");

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer });

        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        var instrument = Assert.Single(loaded.Instruments);
        Assert.Equal("Layer", instrument.Name);
        Assert.Equal(["kick", "clap"], instrument.Sounds.Select(sound => sound.Sound));
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
        Assert.Equal(["kick"], notes[0].Instrument.Sounds.Select(sound => sound.Sound));
        Assert.Equal("snare", notes[2].Instrument.Name);

        // A pre-instrument file must render identically to a hand-built single-sound one.
        var handBuilt = new ThirtyDollarProject { RootTiming = { BPM = 120 } };
        var track = handBuilt.NewTrack();
        var kick = handBuilt.NewInstrument("kick");
        kick.AddSound("kick");
        var snare = handBuilt.NewInstrument("snare");
        snare.AddSound("snare");
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
        layer.AddSound("kick").Value = -3;
        layer.AddSound("snare").Value = -9;

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer, Value = 3 });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;
        Assert.Equal(0, events.Single(e => e.SoundEvent == "kick").Value);
        Assert.Equal(-6, events.Single(e => e.SoundEvent == "snare").Value);
    }

    [Fact]
    public void SoundVolume_ScalesTheNotesVolume_InsteadOfReplacingIt()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        // "kick" is half as loud as the rest of the instrument; "snare" is untouched and
        // should just follow the note.
        layer.AddSound("kick").Volume = 50;
        layer.AddSound("snare");

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer, Volume = 50 });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;
        Assert.Equal(25, events.Single(e => e.SoundEvent == "kick").Volume);
        Assert.Equal(50, events.Single(e => e.SoundEvent == "snare").Volume);
    }

    [Fact]
    public void SoundVolume_OnANoteWithNoVolumeOfItsOwn_PlaysAtTheSoundsVolume()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.AddSound("kick").Volume = 50;

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer }); // Volume null
        project.Place(track, 0, 0);

        Assert.Equal(50, project.ToSequence().Events.Single(e => e.SoundEvent == "kick").Volume);
    }

    [Fact]
    public void SoundAdjustment_RoundTrips_ThroughProjectFile()
    {
        var project = new ThirtyDollarProject();
        var layer = project.NewInstrument("Layer");
        layer.Sounds.Add(new InstrumentSound { Sound = "kick", Value = -3, Volume = 20, Pan = -15 });

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer });

        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        var adjustment = Assert.Single(Assert.Single(loaded.Instruments).Sounds);
        Assert.Equal(-3, adjustment.Value);
        Assert.Equal(20, adjustment.Volume);
        Assert.Equal(-15, adjustment.Pan);
    }

    [Fact]
    public void TheSameSoundTwice_PlaysTwice_EachWithItsOwnTuning()
    {
        // Dual-octave playback: one instrument holding a sound at 0 and again at -12.
        var project = new ThirtyDollarProject();
        var octaves = project.NewInstrument("Octaves");
        octaves.AddSound("kick");
        octaves.AddSound("kick").Value = -12;

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = octaves, Value = 3 });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;
        Assert.Equal([3, -9], events.Where(e => e.SoundEvent == "kick").Select(e => e.Value));
        // Layered on one step, so the second copy is "!combine"d onto the first.
        Assert.Contains(events, e => e.SoundEvent == "!combine");
    }

    [Fact]
    public void TheSameSoundTwice_RoundTripsAsTwoEntries()
    {
        var project = new ThirtyDollarProject();
        var octaves = project.NewInstrument("Octaves");
        octaves.AddSound("kick");
        octaves.AddSound("kick").Value = -12;

        var loaded = Assert.Single(ProjectFile.Load(ProjectFile.Save(project)).Instruments);

        Assert.Equal(["kick", "kick"], loaded.Sounds.Select(sound => sound.Sound));
        Assert.Equal([0, -12], loaded.Sounds.Select(sound => sound.Value));
    }

    [Fact]
    public void TheSameSoundTwice_CutsOnce()
    {
        var project = new ThirtyDollarProject();
        var octaves = project.NewInstrument("Octaves");
        octaves.AddSound("kick");
        octaves.AddSound("kick").Value = -12;

        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = octaves, IsCut = true });
        project.Place(track, 0, 0);

        var cut = Assert.Single(project.ToSequence().Events.OfType<IndividualCutEvent>());
        Assert.Equal(["kick"], cut.CutSounds);
    }

    [Fact]
    public void LegacyFile_BareSoundNamesAndAnAdjustmentMap_MergeIntoInstances()
    {
        // Pre-duplicate-sounds shape: "sounds" holds bare names, tuning sits in a separate
        // map keyed by sound name.
        const string legacy = """
                              {
                                "info": { "name": "Old" },
                                "rootTiming": { "bpm": 120, "numerator": 4, "denominator": 4 },
                                "tracks": [],
                                "instruments": [
                                  {
                                    "id": 1,
                                    "name": "Layer",
                                    "sounds": ["kick", "snare"],
                                    "adjustments": { "kick": { "value": -3, "volume": 20, "pan": -15 } }
                                  }
                                ]
                              }
                              """;

        var instrument = Assert.Single(ProjectFile.Load(legacy).Instruments);

        Assert.Equal(["kick", "snare"], instrument.Sounds.Select(sound => sound.Sound));
        Assert.Equal(-3, instrument.Sounds[0].Value);
        Assert.Equal(20, instrument.Sounds[0].Volume);
        Assert.Equal(-15, instrument.Sounds[0].Pan);
        Assert.Equal(0, instrument.Sounds[1].Value);
        Assert.Null(instrument.Sounds[1].Volume);
    }
}