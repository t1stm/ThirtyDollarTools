namespace ThirtyDollarConverter.Editor.Tests;

public class ThirtyDollarProjectTests
{
    [Fact]
    public void RemoveTrack_RemovesOnlyThatTrack_AndKeepsIdsUnique()
    {
        var project = new ThirtyDollarProject();
        var first = project.NewTrack();
        var second = project.NewTrack();

        Assert.True(project.RemoveTrack(first));
        Assert.False(project.RemoveTrack(first));
        Assert.Equal([second], project.Tracks);

        // The id counter never reuses a removed track's id.
        Assert.Equal(3, project.NewTrack().Id);
    }

    [Fact]
    public void DuplicateTrack_DeepCopiesNotes_SoEditingOneNeverTouchesTheOther()
    {
        var project = new ThirtyDollarProject();
        var instrument = project.NewInstrument("kick");
        var source = project.NewTrack();
        var note = new Note { Step = 0, Instrument = instrument, Value = 1 };
        source.Segments[0].Notes.Add(note);

        var copy = project.DuplicateTrack(source, "Track 1 copy");

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Track 1 copy", copy.Name);
        Assert.NotSame(note, copy.Segments[0].Notes[0]);

        // Mutating the source note must not reach the duplicate, and vice versa.
        note.Value = 99;
        Assert.Equal(1, copy.Segments[0].Notes[0].Value);
        copy.Segments[0].Notes[0].Value = -5;
        Assert.Equal(99, note.Value);
    }
}
