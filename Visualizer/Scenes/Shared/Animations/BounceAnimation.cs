using OpenTK.Mathematics;
using Sundex.Core;
using Sundex.Core.Animations;

namespace Shared.Animations;

public class BounceAnimation : Animation
{
    private const int AnimationLengthMs = 400;
    public float FinalY;

    public BounceAnimation() : base(AnimationLengthMs)
    {
        Features = AnimationFeature.TransformAdd;
    }

    public BounceAnimation(Action finishCallback) : this()
    {
        CallbackOnFinish = finishCallback;
    }

    public override Vector3 GetTransform_Add(Renderable renderable)
    {
        if (!TimingStopwatch.IsRunning && LoopingMode == AnimationLoopingMode.None)
            return base.GetTransform_Add(renderable);

        var transformation = new Vector3();

        var elapsed = (float)TimingStopwatch.ElapsedMilliseconds;
        var totalLength = (float)AnimationLength.TotalMilliseconds;

        if (totalLength > 0)
            switch (LoopingMode)
            {
                case AnimationLoopingMode.ResetToStart:
                    elapsed %= totalLength;
                    break;
                case AnimationLoopingMode.Invert:
                    var loopCount = (int)(elapsed / totalLength);
                    elapsed %= totalLength;
                    if (loopCount % 2 == 1) elapsed = totalLength - elapsed;
                    break;
                case AnimationLoopingMode.None:
                case AnimationLoopingMode.LoopStart:
                default:
                    if (elapsed >= totalLength)
                    {
                        if (TimingStopwatch.IsRunning)
                        {
                            TimingStopwatch.Stop();
                            CallbackOnFinish?.Invoke();
                        }

                        elapsed = totalLength;
                    }

                    break;
            }

        var normalized = totalLength > 0 ? Math.Max(elapsed / totalLength, 0) : 1f;
        if (normalized > 1) normalized = 1;

        float factor;

        // Animation: % max, ease out on start, ease in after %
        const float maxPercent = 0.4f;
        if (normalized < maxPercent)
        {
            var temp_val = normalized / maxPercent / 2f;
            factor = MathF.Sin(MathF.PI * temp_val);
        }
        else
        {
            var temp_val = 0.5f + (normalized - maxPercent) / (1f - maxPercent) * 0.5f;
            factor = 1f - MathF.Cos(MathF.PI * (1f - temp_val));
        }

        transformation.Y = -factor * FinalY;

        return transformation;
    }
}