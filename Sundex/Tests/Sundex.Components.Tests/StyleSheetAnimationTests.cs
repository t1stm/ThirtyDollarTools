using Sundex.Components.Abstractions;
using Sundex.Core.Animations;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

public class StyleSheetAnimationTests
{
    [Fact]
    public void TestApplyStyleSheet_AnimationsAreApplied()
    {
        var context = new TestUIContext();
        var element = new TestElement(context);

        // 1. Setup a StyleSheet with a computed animation
        var holder = new StyleSheetHolder();
        const string animationName = "fade-in";
        var keyframe = new Keyframe { Opacity = 1, LengthMs = 100 };
        var animation = new KeyframedAnimation(new List<Keyframe> { keyframe });

        // We can't easily populate holder.Animations and call ParseAnimations because it's complex.
        // But we can create the StyleSheet and then inject into ComputedAnimations if needed, 
        // OR we can mock the holder.

        // Let's use the actual StyleSheet constructor but we need to satisfy holder.Animations 
        // if we want it to parse properly, or just use reflection/internal to inject.
        // Actually, ComputedAnimations is public getter.

        var styleSheet = new StyleSheet(holder)
        {
            ComputedAnimations =
            {
                [animationName] = animation
            }
        };

        // 2. Setup the component style with the animation name
        holder.Components[element.Tag] = new Dictionary<string, IStyleValue>
        {
            ["animations"] = new ArrayValue([new StringValue(animationName)])
        };

        // 3. Apply the style sheet
        element.ApplyStyleSheet(styleSheet);

        // 4. Verify - the element gets an equivalent animation, not the sheet's own object.
        Assert.Single(element.Animations);
        var applied = Assert.IsType<KeyframedAnimation>(element.Animations[0]);
        Assert.NotSame(animation, applied);
        Assert.Equal(animation.Keyframes, applied.Keyframes);
        Assert.Equal(animation.Features, applied.Features);
        Assert.Equal(animation.LoopingMode, applied.LoopingMode);
    }

    [Fact]
    public void TestApplyStyleSheet_ElementsMatchingOneRule_GetIndependentClocks()
    {
        var context = new TestUIContext();
        var first = new TestElement(context);
        var second = new TestElement(context);

        var holder = new StyleSheetHolder();
        const string animationName = "fade-in";
        var animation = new KeyframedAnimation([
            new Keyframe { Opacity = 0, LengthMs = 100 },
            new Keyframe { Opacity = 1, LengthMs = 100 }
        ]);

        var styleSheet = new StyleSheet(holder)
        {
            ComputedAnimations =
            {
                [animationName] = animation
            }
        };

        holder.Components[first.Tag] = new Dictionary<string, IStyleValue>
        {
            ["animations"] = new ArrayValue([new StringValue(animationName)])
        };

        first.ApplyStyleSheet(styleSheet);
        second.ApplyStyleSheet(styleSheet);

        Assert.NotSame(first.Animations[0], second.Animations[0]);

        // Advancing one element's animation must not move the other's: a shared
        // KeyframedAnimation carries one TimingStopwatch, which played every element
        // matching the rule in lockstep.
        first.Animations[0].TimingStopwatch.Seek(150);

        Assert.Equal(150, first.Animations[0].TimingStopwatch.ElapsedMilliseconds);
        Assert.Equal(0, second.Animations[0].TimingStopwatch.ElapsedMilliseconds);
        Assert.Equal(0, animation.TimingStopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public void Animations_StartOnFirstDrawTo_NotOnStyleAssignment()
    {
        var context = new TestUIContext();
        var panel = new TestPanel(context);

        var animation = new KeyframedAnimation([new Keyframe { Opacity = 1, LengthMs = 100 }]);
        panel.Animations = [animation];

        // A tree is commonly styled long before it is shown; the clock must not run yet.
        Assert.False(animation.TimingStopwatch.IsRunning);

        panel.DrawTo(context);
        Assert.True(animation.TimingStopwatch.IsRunning);

        // A later draw must not rewind it - DrawTo runs again on re-show, AddChild and
        // the Visible setter.
        animation.TimingStopwatch.Seek(80);
        panel.DrawTo(context);
        Assert.Equal(80, animation.TimingStopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public void AddAnimation_AfterFirstDraw_StartsImmediately()
    {
        var context = new TestUIContext();
        var panel = new TestPanel(context);
        panel.DrawTo(context);

        var animation = new KeyframedAnimation([new Keyframe { Opacity = 1, LengthMs = 100 }]);
        panel.AddAnimation(animation);

        // Appending bypasses the Animations setter, so the element's start pass has gone.
        Assert.True(animation.TimingStopwatch.IsRunning);

        animation.TimingStopwatch.Seek(40);
        panel.DrawTo(context);
        Assert.Equal(40, animation.TimingStopwatch.ElapsedMilliseconds);
    }

    private class TestElement : UIElement
    {
        public TestElement(UIContext context) : base(context)
        {
        }

        public override string Tag => "test-element";

        protected override void DrawSelf(UIContext context)
        {
        }
    }
}