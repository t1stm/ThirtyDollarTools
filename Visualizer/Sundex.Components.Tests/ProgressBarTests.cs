using Sundex.Components.Bars;

namespace Sundex.Components.Tests;

public class ProgressBarTests
{
    [Fact]
    public void TestProgressBar_ProgressUpdatesWidth()
    {
        var context = new TestUIContext();
        var progressBar = new ProgressBar(context)
        {
            Width = 100,
            Height = 20,
            Progress = 0.5f
        };

        progressBar.Layout();

        Assert.Equal(100, progressBar.BackgroundPanel.Width);
        Assert.Equal(50, progressBar.ForegroundPanel.Width);
        Assert.Equal(20, progressBar.ForegroundPanel.Height);
    }

    [Fact]
    public void TestProgressBar_InvalidatesWhenProgressChanges()
    {
        var context = new TestUIContext();
        var progressBar = new ProgressBar(context)
        {
            Width = 100,
            Height = 20,
            Progress = 0.5f
        };

        progressBar.Layout();
        Assert.False(progressBar.NeedsLayout);

        progressBar.Progress = 0.8f;
        Assert.True(progressBar.NeedsLayout);
        Assert.True(progressBar.ForegroundPanel.NeedsLayout);
    }
}