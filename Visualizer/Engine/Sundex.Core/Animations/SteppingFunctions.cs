using OpenTK.Mathematics;

namespace Sundex.Core.Animations;

public static class SteppingFunctions
{
    public static Vector3 Exponential(Vector3 current, Vector3 target, double deltaSeconds, float distanceSnap = 0.01f,
        float speed = 7.5f)
    {
        // exponentional smoothing by lisyarus
        // https://lisyarus.github.io/blog/posts/exponential-smoothing.html

        if (Vector3.Distance(current, target) < distanceSnap) return target;

        current += (target - current) * (float)(1f - Math.Exp(-speed * deltaSeconds));
        return current;
    }

    public static float Apply(SteppingFunction function, float t)
    {
        return function switch
        {
            SteppingFunction.Linear => t,
            SteppingFunction.Exponential => t >= 1f ? 1f : 1f - MathF.Exp(-7.5f * t),
            SteppingFunction.SineIn => 1f - MathF.Cos(t * MathF.PI / 2f),
            SteppingFunction.SineOut => MathF.Sin(t * MathF.PI / 2f),
            SteppingFunction.SineInOut => -(MathF.Cos(MathF.PI * t) - 1f) / 2f,
            SteppingFunction.QuadIn => t * t,
            SteppingFunction.QuadOut => 1f - (1f - t) * (1f - t),
            SteppingFunction.QuadInOut => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f,
            SteppingFunction.CubicIn => t * t * t,
            SteppingFunction.CubicOut => 1f - MathF.Pow(1f - t, 3f),
            SteppingFunction.CubicInOut => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f,
            SteppingFunction.QuartIn => t * t * t * t,
            SteppingFunction.QuartOut => 1f - MathF.Pow(1f - t, 4f),
            SteppingFunction.QuartInOut => t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) / 2f,
            SteppingFunction.QuintIn => t * t * t * t * t,
            SteppingFunction.QuintOut => 1f - MathF.Pow(1f - t, 5f),
            SteppingFunction.QuintInOut => t < 0.5f ? 16f * t * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 5f) / 2f,
            SteppingFunction.ExpoIn => Math.Abs(t) < 0.001f ? 0f : MathF.Pow(2f, 10f * t - 10f),
            SteppingFunction.ExpoOut => Math.Abs(t - 1f) < 0.001f ? 1f : 1f - MathF.Pow(2f, -10f * t),
            SteppingFunction.ExpoInOut => Math.Abs(t) < 0.001f ? 0f :
                Math.Abs(t - 1f) < 0.001f ? 1f :
                t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) / 2f : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f,
            SteppingFunction.CircIn => 1f - MathF.Sqrt(1f - MathF.Pow(t, 2f)),
            SteppingFunction.CircOut => MathF.Sqrt(1f - MathF.Pow(t - 1f, 2f)),
            SteppingFunction.CircInOut => t < 0.5f
                ? (1f - MathF.Sqrt(1f - MathF.Pow(2f * t, 2f))) / 2f
                : (MathF.Sqrt(1f - MathF.Pow(-2f * t + 2f, 2f)) + 1f) / 2f,
            SteppingFunction.BackIn => 2.70158f * t * t * t - 1.70158f * t * t,
            SteppingFunction.BackOut => 1f + 2.70158f * MathF.Pow(t - 1f, 3f) + 1.70158f * MathF.Pow(t - 1f, 2f),
            SteppingFunction.BackInOut => t < 0.5f
                ? MathF.Pow(2f * t, 2f) * ((2.59491f + 1f) * 2f * t - 2.59491f) / 2f
                : (MathF.Pow(2f * t - 2f, 2f) * ((2.59491f + 1f) * (t * 2f - 2f) + 2.59491f) + 2f) / 2f,
            SteppingFunction.ElasticIn => Math.Abs(t) < 0.001f ? 0f :
                Math.Abs(t - 1f) < 0.001f ? 1f :
                -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * (2f * MathF.PI / 3f)),
            SteppingFunction.ElasticOut => Math.Abs(t) < 0.001f ? 0f :
                Math.Abs(t - 1f) < 0.001f ? 1f :
                MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * (2f * MathF.PI / 3f)) + 1f,
            SteppingFunction.ElasticInOut => Math.Abs(t) < 0.001f ? 0f :
                Math.Abs(t - 1f) < 0.001f ? 1f :
                t < 0.5f ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * (2f * MathF.PI / 4.5f))) / 2f :
                MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * (2f * MathF.PI / 4.5f)) / 2f + 1f,
            SteppingFunction.BounceIn => 1f - BounceOut(1f - t),
            SteppingFunction.BounceOut => BounceOut(t),
            SteppingFunction.BounceInOut => t < 0.5f ? (1f - BounceOut(1f - 2f * t)) / 2f : (1f + BounceOut(2f * t - 1f)) / 2f,
            _ => t
        };
    }

    private static float BounceOut(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }
}