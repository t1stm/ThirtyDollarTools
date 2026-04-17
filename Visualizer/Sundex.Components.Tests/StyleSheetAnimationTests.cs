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

        // 4. Verify
        Assert.Single(element.Animations);
        Assert.Equal(animation, element.Animations[0]);
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