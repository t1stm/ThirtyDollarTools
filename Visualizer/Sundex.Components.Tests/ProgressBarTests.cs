using Xunit;
using Sundex.Components.Bars;
using Sundex.Components.Panels;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using OpenTK.Mathematics;
using System.Collections.Generic;
using Sundex.Engine.Renderer.Abstract;
using Shared;
using Sundex.Engine.Renderer.Cameras;

namespace Sundex.Components.Tests;

public class ProgressBarTests
{
    private class TestContext : UIContext
    {
        public TestContext()
        {
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        }
        public List<List<IRenderable>> GetRenderQueue() => LayeredRenderQueue;
    }

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
        bool found = false;
        foreach (var layer in queue)
        {
            foreach (var item in layer)
            {
                if (item == progressBar.BackgroundPanel.Background)
                {
                    found = true;
                    break;
                }
            }
        }
        Assert.True(found, "New BackgroundPanel's background should be in the render queue");
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
            Background = null // Simplified
        };
        // Resetting to zero like it happens when new Panel() is called and style value is applied
        bgPanel.Height = 0; 
        
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
}

public static class ProgressBarTestExtensions
{
    public static void TestApplyStyleValue(this ProgressBar bar, StyleSheet styleSheet, IStyleValue? styleValue, System.Reflection.PropertyInfo propertyInfo)
    {
        var method = typeof(ProgressBar).GetMethod("ApplyStyleValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method!.Invoke(bar, new object?[] { styleSheet, styleValue, propertyInfo });
    }
}
