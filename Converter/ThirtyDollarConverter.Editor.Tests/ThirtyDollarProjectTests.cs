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
}
