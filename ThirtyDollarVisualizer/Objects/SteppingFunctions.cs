using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public static class SteppingFunctions
{
    public static Vector3 Exponential(Vector3 current, Vector3 target, float deltaSeconds, float distanceSnap = 0.01f,
        float speed = 7.5f)
    {
        // exponentional smoothing by lisyarus
        // https://lisyarus.github.io/blog/posts/exponential-smoothing.html

        if (Vector3.Distance(current, target) < distanceSnap) return target;

        current += (target - current) * (1f - MathF.Exp(-speed * deltaSeconds));
        return current;
    }
}