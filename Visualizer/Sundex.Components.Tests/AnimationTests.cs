using Shared.Animations;
using OpenTK.Mathematics;
using Xunit;
using System.Reflection;
using Sundex.Core;
using Sundex.Core.Animations;

namespace Sundex.Components.Tests;

public class AnimationTests
{
    [Fact]
    public void TestLoopStartBehavior()
    {
        // Setup positions:
        // P0: (0,0,0), 1000ms, None
        // P1: (1,1,1), 1000ms, None
        // P2: (2,2,2), 1000ms, LoopStart  <-- Loop starts here (at 2000ms)
        // P3: (3,3,3), 1000ms, None
        // P4: (4,4,4), 1000ms, None
        // P5: (5,5,5), 1000ms, Invert     <-- Loop ends here (at 5000ms)
        // P6: (6,6,6), 1000ms, None

        var positions = new[]
        {
            new Keyframe { Position = new Vector3(0), LengthMs = 1000, LoopingMode = AnimationLoopingMode.None },
            new Keyframe { Position = new Vector3(1), LengthMs = 1000, LoopingMode = AnimationLoopingMode.None },
            new Keyframe { Position = new Vector3(2), LengthMs = 1000, LoopingMode = AnimationLoopingMode.LoopStart },
            new Keyframe { Position = new Vector3(3), LengthMs = 1000, LoopingMode = AnimationLoopingMode.None },
            new Keyframe { Position = new Vector3(4), LengthMs = 1000, LoopingMode = AnimationLoopingMode.None },
            new Keyframe { Position = new Vector3(5), LengthMs = 1000, LoopingMode = AnimationLoopingMode.Invert },
            new Keyframe { Position = new Vector3(6), LengthMs = 1000, LoopingMode = AnimationLoopingMode.None }
        };

        var animation = new KeyframedAnimation(positions);
        
        // LoopStart at 2000ms
        // Invert at 5000ms
        // Loop duration = 3000ms
        
        // We need to simulate time. Since TimingStopwatch is protected/internal-ish, 
        // we might need to use reflection or a wrapper if we can't control it.
        // Looking at SeekableStopwatch, it has Seek(long delta).
        
        var stopwatchField = typeof(Animation).GetProperty("TimingStopwatch", BindingFlags.NonPublic | BindingFlags.Instance);
        var stopwatch = (SeekableStopwatch)stopwatchField.GetValue(animation);

        // 1. Initial play (before LoopStart)
        stopwatch.Seek(500);
        var transform = animation.GetTransform_Add(null);
        Assert.Equal(Vector3.Lerp(new Vector3(0), new Vector3(1), 0.5f), transform);

        // 2. Just before Invert
        stopwatch.Seek(4500);
        transform = animation.GetTransform_Add(null);
        Assert.Equal(Vector3.Lerp(new Vector3(4), new Vector3(5), 0.5f), transform);

        // 3. Exactly at Invert
        stopwatch.Seek(5000);
        transform = animation.GetTransform_Add(null);
        Assert.Equal(new Vector3(5), transform);

        // 4. After Invert (should be playing backwards)
        // 5500ms total. 
        // loopElapsed = (5500 - 2000) % 6000 = 3500.
        // 3500 > 3000, so backwards.
        // elapsed = 5000 - (3500 - 3000) = 4500.
        stopwatch.Seek(5500);
        transform = animation.GetTransform_Add(null);
        Assert.Equal(Vector3.Lerp(new Vector3(4), new Vector3(5), 0.5f), transform);

        // 5. Back to LoopStart
        // 8000ms total.
        // loopElapsed = (8000 - 2000) % 6000 = 0.
        // elapsed = 2000 + 0 = 2000.
        stopwatch.Seek(8000);
        transform = animation.GetTransform_Add(null);
        Assert.Equal(new Vector3(2), transform);

        // 6. Loop again forward
        // 8500ms total.
        // loopElapsed = (8500 - 2000) % 6000 = 500.
        // elapsed = 2000 + 500 = 2500.
        stopwatch.Seek(8500);
        transform = animation.GetTransform_Add(null);
        Assert.Equal(Vector3.Lerp(new Vector3(2), new Vector3(3), 0.5f), transform);
    }

    [Fact]
    public void TestSteppingFunctions()
    {
        var positions = new[]
        {
            new Keyframe
            {
                Position = new Vector3(0),
                LengthMs = 1000,
                SteppingFunction = SteppingFunction.QuadIn
            },
            new Keyframe
            {
                Position = new Vector3(1),
                LengthMs = 1000
            }
        };

        var animation = new KeyframedAnimation(positions);
        var stopwatchField = typeof(Animation).GetProperty("TimingStopwatch", BindingFlags.NonPublic | BindingFlags.Instance);
        var stopwatch = (SeekableStopwatch)stopwatchField.GetValue(animation);

        // At 500ms, progress is 0.5.
        // QuadIn(0.5) = 0.5 * 0.5 = 0.25.
        stopwatch.Seek(500);
        var transform = animation.GetTransform_Add(null);
        
        // Expected position = Lerp(0, 1, 0.25) = 0.25
        Assert.Equal(new Vector3(0.25f), transform);
    }

    [Fact]
    public void TestLoopStartResetToStartBehavior()
    {
        // Setup positions:
        // P0: (0,0,0), 1000ms
        // P1: (1,1,1), 1000ms, LoopStart
        // P2: (2,2,2), 1000ms
        
        var positions = new[]
        {
            new Keyframe { Position = new Vector3(0), LengthMs = 1000 },
            new Keyframe { Position = new Vector3(1), LengthMs = 1000, LoopingMode = AnimationLoopingMode.LoopStart },
            new Keyframe { Position = new Vector3(2), LengthMs = 1000 }
        };

        var animation = new KeyframedAnimation(positions);
        var stopwatchField = typeof(Animation).GetProperty("TimingStopwatch", BindingFlags.NonPublic | BindingFlags.Instance);
        var stopwatch = (SeekableStopwatch)stopwatchField.GetValue(animation);

        // totalLength = 3000ms
        // loopStart = 1000ms
        // loopDuration = 2000ms

        // 1. Initial play
        stopwatch.Seek(500);
        Assert.Equal(Vector3.Lerp(new Vector3(0), new Vector3(1), 0.5f), animation.GetTransform_Add(null));

        // 2. Loop play
        // 3500ms -> (3500-1000) % 2000 = 500. elapsed = 1000 + 500 = 1500ms
        stopwatch.Seek(3500);
        Assert.Equal(Vector3.Lerp(new Vector3(1), new Vector3(2), 0.5f), animation.GetTransform_Add(null));
        
        // 3. Loop play later
        // 5500ms -> (5500-1000) % 2000 = 500. elapsed = 1000 + 500 = 1500ms
        stopwatch.Seek(5500);
        Assert.Equal(Vector3.Lerp(new Vector3(1), new Vector3(2), 0.5f), animation.GetTransform_Add(null));
    }

    [Fact]
    public void TestColorInterpolation()
    {
        var positions = new[]
        {
            new Keyframe
            {
                Color = new Vector4(1, 0, 0, 1), // Red
                LengthMs = 1000
            },
            new Keyframe
            {
                Color = new Vector4(0, 0, 1, 1), // Blue
                LengthMs = 1000
            }
        };

        var animation = new KeyframedAnimation(positions);
        var stopwatchField = typeof(Animation).GetProperty("TimingStopwatch", BindingFlags.NonPublic | BindingFlags.Instance);
        var stopwatch = (SeekableStopwatch)stopwatchField.GetValue(animation);

        // At 500ms, progress is 0.5
        stopwatch.Seek(500);
        var color = animation.GetColor_Value(null);
        
        // Expected color = Lerp(Red, Blue, 0.5) = (0.5, 0, 0.5, 1)
        Assert.Equal(new Vector4(0.5f, 0, 0.5f, 1), color);
    }
}
