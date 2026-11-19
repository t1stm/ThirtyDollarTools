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

    public static float Apply(SteppingFunction function, float progress)
    {
        return function switch
        {
            SteppingFunction.Linear => progress,
            SteppingFunction.Exponential => progress >= 1f ? 1f : 1f - MathF.Exp(-7.5f * progress),
            SteppingFunction.SineIn => 1f - MathF.Cos(progress * MathF.PI / 2f),
            SteppingFunction.SineOut => MathF.Sin(progress * MathF.PI / 2f),
            SteppingFunction.SineInOut => -(MathF.Cos(MathF.PI * progress) - 1f) / 2f,
            SteppingFunction.QuadIn => progress * progress,
            SteppingFunction.QuadOut => 1f - (1f - progress) * (1f - progress),
            SteppingFunction.QuadInOut => progress < 0.5f ? 2f * progress * progress : 1f - MathF.Pow(-2f * progress + 2f, 2f) / 2f,
            SteppingFunction.CubicIn => progress * progress * progress,
            SteppingFunction.CubicOut => 1f - MathF.Pow(1f - progress, 3f),
            SteppingFunction.CubicInOut => progress < 0.5f ? 4f * progress * progress * progress : 1f - MathF.Pow(-2f * progress + 2f, 3f) / 2f,
            SteppingFunction.QuartIn => progress * progress * progress * progress,
            SteppingFunction.QuartOut => 1f - MathF.Pow(1f - progress, 4f),
            SteppingFunction.QuartInOut => progress < 0.5f ? 8f * progress * progress * progress * progress : 1f - MathF.Pow(-2f * progress + 2f, 4f) / 2f,
            SteppingFunction.QuintIn => progress * progress * progress * progress * progress,
            SteppingFunction.QuintOut => 1f - MathF.Pow(1f - progress, 5f),
            SteppingFunction.QuintInOut => progress < 0.5f ? 16f * progress * progress * progress * progress * progress : 1f - MathF.Pow(-2f * progress + 2f, 5f) / 2f,
            SteppingFunction.ExpoIn => Math.Abs(progress) < 0.001f ? 0f : MathF.Pow(2f, 10f * progress - 10f),
            SteppingFunction.ExpoOut => Math.Abs(progress - 1f) < 0.001f ? 1f : 1f - MathF.Pow(2f, -10f * progress),
            SteppingFunction.ExpoInOut => Math.Abs(progress) < 0.001f ? 0f :
                Math.Abs(progress - 1f) < 0.001f ? 1f :
                progress < 0.5f ? MathF.Pow(2f, 20f * progress - 10f) / 2f : (2f - MathF.Pow(2f, -20f * progress + 10f)) / 2f,
            SteppingFunction.CircIn => 1f - MathF.Sqrt(1f - MathF.Pow(progress, 2f)),
            SteppingFunction.CircOut => MathF.Sqrt(1f - MathF.Pow(progress - 1f, 2f)),
            SteppingFunction.CircInOut => progress < 0.5f
                ? (1f - MathF.Sqrt(1f - MathF.Pow(2f * progress, 2f))) / 2f
                : (MathF.Sqrt(1f - MathF.Pow(-2f * progress + 2f, 2f)) + 1f) / 2f,
            SteppingFunction.BackIn => 2.70158f * progress * progress * progress - 1.70158f * progress * progress,
            SteppingFunction.BackOut => 1f + 2.70158f * MathF.Pow(progress - 1f, 3f) + 1.70158f * MathF.Pow(progress - 1f, 2f),
            SteppingFunction.BackInOut => progress < 0.5f
                ? MathF.Pow(2f * progress, 2f) * ((2.59491f + 1f) * 2f * progress - 2.59491f) / 2f
                : (MathF.Pow(2f * progress - 2f, 2f) * ((2.59491f + 1f) * (progress * 2f - 2f) + 2.59491f) + 2f) / 2f,
            SteppingFunction.ElasticIn => Math.Abs(progress) < 0.001f ? 0f :
                Math.Abs(progress - 1f) < 0.001f ? 1f :
                -MathF.Pow(2f, 10f * progress - 10f) * MathF.Sin((progress * 10f - 10.75f) * (2f * MathF.PI / 3f)),
            SteppingFunction.ElasticOut => Math.Abs(progress) < 0.001f ? 0f :
                Math.Abs(progress - 1f) < 0.001f ? 1f :
                MathF.Pow(2f, -10f * progress) * MathF.Sin((progress * 10f - 0.75f) * (2f * MathF.PI / 3f)) + 1f,
            SteppingFunction.ElasticInOut => Math.Abs(progress) < 0.001f ? 0f :
                Math.Abs(progress - 1f) < 0.001f ? 1f :
                progress < 0.5f ? -(MathF.Pow(2f, 20f * progress - 10f) * MathF.Sin((20f * progress - 11.125f) * (2f * MathF.PI / 4.5f))) /
                           2f :
                MathF.Pow(2f, -20f * progress + 10f) * MathF.Sin((20f * progress - 11.125f) * (2f * MathF.PI / 4.5f)) / 2f + 1f,
            SteppingFunction.BounceIn => 1f - BounceOut(1f - progress),
            SteppingFunction.BounceOut => BounceOut(progress),
            SteppingFunction.BounceInOut => progress < 0.5f
                ? (1f - BounceOut(1f - 2f * progress)) / 2f
                : (1f + BounceOut(2f * progress - 1f)) / 2f,
            _ => progress
        };
    }

    public static SteppingFunction ParseSteppingFunction(ReadOnlySpan<char> steppingFunctionString)
    {
        return steppingFunctionString switch
        {
            // Generic/ease aliases
            "linear" => SteppingFunction.Linear,
            "ease-in" => SteppingFunction.SineIn,
            "ease-out" => SteppingFunction.SineOut,
            "ease-in-out" => SteppingFunction.SineInOut,

            // Sine
            "sine-in" => SteppingFunction.SineIn,
            "sine-out" => SteppingFunction.SineOut,
            "sine-in-out" => SteppingFunction.SineInOut,

            // Quadratic
            "quad-in" => SteppingFunction.QuadIn,
            "quad-out" => SteppingFunction.QuadOut,
            "quad-in-out" => SteppingFunction.QuadInOut,

            // Cubic
            "cubic-in" => SteppingFunction.CubicIn,
            "cubic-out" => SteppingFunction.CubicOut,
            "cubic-in-out" => SteppingFunction.CubicInOut,

            // Quartic
            "quart-in" => SteppingFunction.QuartIn,
            "quart-out" => SteppingFunction.QuartOut,
            "quart-in-out" => SteppingFunction.QuartInOut,

            // Quintic
            "quint-in" => SteppingFunction.QuintIn,
            "quint-out" => SteppingFunction.QuintOut,
            "quint-in-out" => SteppingFunction.QuintInOut,

            // Exponential (aliases + explicit expo variants)
            "exponential" => SteppingFunction.Exponential,
            "expo-in" => SteppingFunction.ExpoIn,
            "expo-out" => SteppingFunction.ExpoOut,
            "expo-in-out" => SteppingFunction.ExpoInOut,

            // Circular
            "circ-in" => SteppingFunction.CircIn,
            "circ-out" => SteppingFunction.CircOut,
            "circ-in-out" => SteppingFunction.CircInOut,

            // Back
            "back-in" => SteppingFunction.BackIn,
            "back-out" => SteppingFunction.BackOut,
            "back-in-out" => SteppingFunction.BackInOut,

            // Elastic
            "elastic-in" => SteppingFunction.ElasticIn,
            "elastic-out" => SteppingFunction.ElasticOut,
            "elastic-in-out" => SteppingFunction.ElasticInOut,

            // Bounce
            "bounce-in" => SteppingFunction.BounceIn,
            "bounce-out" => SteppingFunction.BounceOut,
            "bounce-in-out" => SteppingFunction.BounceInOut,

            _ => throw new Exception($"Unknown stepping function: {steppingFunctionString}")
        };
    }

    private static float BounceOut(float progress)
    {
        const float bounce = 7.5625f;
        const float period = 2.75f;

        return progress switch
        {
            < 1f / period => bounce * progress * progress,
            < 2f / period => bounce * (progress -= 1.5f / period) * progress + 0.75f,
            < 2.5f / period => bounce * (progress -= 2.25f / period) * progress + 0.9375f,
            _ => bounce * (progress -= 2.625f / period) * progress + 0.984375f
        };
    }
}