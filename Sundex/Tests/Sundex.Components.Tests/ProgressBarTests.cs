using OpenTK.Mathematics;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

public class ProgressBarTests
{
    [Fact]
    public void ProgressBar_ApplyStyleValue_ShouldQueuePanelBackground()
    {
        // Arrange
        var camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        var context = new TestContext { Camera = camera };
        var progressBar = new ProgressBar(context);
        var backgroundProperty = typeof(ProgressBar).GetProperty(nameof(ProgressBar.BackgroundPanel))!;

        var colorValue = new ColorValue("#FF0000"); // Red

        // Act
        // This will trigger ProgressBar.ApplyStyleValue
        progressBar.TestApplyStyleValue(new StyleSheet(new StyleSheetHolder()), colorValue, backgroundProperty);

        // Assert
        var queue = context.GetRenderQueue();
        var found = queue.Any(layer => layer.Any(item => item == progressBar.BackgroundPanel.Background));
        ;

        Assert.True(found, "New BackgroundPanel's background should be in the render queue");
        Assert.Equal(progressBar, progressBar.BackgroundPanel.Parent);

        var barIndex = progressBar.Index;
        var panelIndex = progressBar.BackgroundPanel.Index;
        Assert.Equal(barIndex + 1, panelIndex);
    }

    [Fact]
    public void ProgressBar_DrawTo_ShouldReQueueRenderablesAfterClear()
    {
        // Arrange
        var camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        var context = new TestContext { Camera = camera };
        var progressBar = new ProgressBar(context, new ColoredPlane { Color = Vector4.One },
            new ColoredPlane { Color = Vector4.UnitX });

        // Initial draw
        progressBar.DrawTo(context);

        // Act
        context.Clear();
        progressBar.DrawTo(context);

        // Assert
        var queue = context.GetRenderQueue();
        var bgLayer = -1;
        var fgLayer = -1;
        var bgPos = -1;
        var fgPos = -1;

        for (var i = 0; i < queue.Count; i++)
        for (var j = 0; j < queue[i].Count; j++)
        {
            if (queue[i][j] == progressBar.BackgroundPanel.Background)
            {
                bgLayer = i;
                bgPos = j;
            }

            if (queue[i][j] != progressBar.ForegroundPanel.Background) continue;
            fgLayer = i;
            fgPos = j;
        }

        Assert.True(bgLayer != -1, "Background should be re-queued after Clear()");
        Assert.True(fgLayer != -1, "Foreground should be re-queued after Clear()");

        var barIndex = progressBar.Index;
        var bgIndex = progressBar.BackgroundPanel.Index;
        var fgIndex = progressBar.ForegroundPanel.Index;

        Assert.Equal(barIndex + 1, bgIndex);
        Assert.Equal(barIndex + 2, fgIndex);

        if (bgLayer == fgLayer)
            Assert.True(fgPos > bgPos, "Foreground should be queued after background in the same layer");
        else
            Assert.True(fgLayer > bgLayer, "Foreground layer should be above background layer");
    }

    [Fact]
    public void ProgressBar_ShouldSetPanelHeights()
    {
        // Arrange
        var camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        var context = new TestContext { Camera = camera };
        var progressBar = new ProgressBar(context);

        // Initial state from constructor (default 100%)
        Assert.Equal(100, progressBar.BackgroundPanel.Height.Value);
        Assert.True(progressBar.BackgroundPanel.Height.IsPercentage);

        // Mocking style apply like DSL does
        // This simulates 'background = #2a2e3a' in DSL
        var bgPanel = new Panel(context)
        {
            Background = null,
            // Resetting to zero like it happens when new Panel() is called and style value is applied
            Height = 0 // Simplified
        };

        progressBar.BackgroundPanel = bgPanel;

        // Act
        progressBar.Height = 16;
        progressBar.Width = 600;
        progressBar.Layout(); // This calls DoLayout internally if needed

        // Assert
        Assert.Equal(100, progressBar.BackgroundPanel.Height.Value);
        Assert.True(progressBar.BackgroundPanel.Height.IsPercentage);
        Assert.Equal(100, progressBar.BackgroundPanel.Width.Value);
        Assert.True(progressBar.BackgroundPanel.Width.IsPercentage);
        Assert.Equal(100, progressBar.ForegroundPanel.Height.Value);
        Assert.True(progressBar.ForegroundPanel.Height.IsPercentage);

        // Verify computed height of panels matches progress bar height
        Assert.Equal(16, progressBar.BackgroundPanel.Computed.Height);
        Assert.Equal(16, progressBar.ForegroundPanel.Computed.Height);
    }

    private class TestContext : UIContext
    {
        public TestContext()
        {
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        }

        public List<List<IRenderable>> GetRenderQueue()
        {
            return LayeredRenderQueue;
        }
    }
}