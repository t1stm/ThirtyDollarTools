namespace EditorScene.Scenes.Components;

/// <summary>
///     A CSS <c>cubic-bezier(x1, y1, x2, y2)</c> timing function. The header's motion was tuned
///     as CSS transitions in the design canvas, so it is replayed with the same curves rather
///     than an approximation of them.
/// </summary>
public readonly record struct CubicBezier(float X1, float Y1, float X2, float Y2)
{
    /// <summary>CSS's <c>ease</c>, the curve a transition gets when it names none.</summary>
    public static readonly CubicBezier Ease = new(0.25f, 0.1f, 0.25f, 1f);

    /// <summary>The eased progress at <paramref name="x" /> (0..1) of the way through the time.</summary>
    public float At(float x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        // x(t) only ever rises for a timing function (both x control points lie in 0..1), so
        // a bisection finds the t that lands on x. 24 halvings is well under a pixel.
        float low = 0, high = 1, t = x;
        for (var i = 0; i < 24; i++)
        {
            if (Sample(X1, X2, t) < x) low = t;
            else high = t;
            t = (low + high) / 2;
        }

        return Sample(Y1, Y2, t);
    }

    /// <summary>One coordinate of the curve from (0,0) through the two control points to (1,1).</summary>
    private static float Sample(float p1, float p2, float t)
    {
        return ((1 - 3 * p2 + 3 * p1) * t + (3 * p2 - 6 * p1)) * t * t + 3 * p1 * t;
    }
}

/// <summary>
///     One value animated the way a CSS transition runs it: a new target starts from wherever
///     the value is at that moment, holds for the delay, then eases there over the duration.
///     Times are seconds on the caller's clock, so a test can step it without a Stopwatch.
/// </summary>
public sealed class Transition(float value)
{
    private float _from = value, _to = value, _start, _duration, _delay;
    private CubicBezier _curve = CubicBezier.Ease;

    /// <summary>Heads for <paramref name="target" />. Asking for the target it already has changes nothing, as in CSS.</summary>
    public void Set(float target, float now, float duration, CubicBezier curve, float delay = 0)
    {
        if (Math.Abs(target - _to) < 1e-6f) return;
        _from = Value(now);
        _to = target;
        _start = now;
        _duration = duration;
        _delay = delay;
        _curve = curve;
    }

    /// <summary>Jumps straight to <paramref name="target" />, cancelling any run in progress.</summary>
    public void Snap(float target)
    {
        _from = _to = target;
        _start = float.NegativeInfinity;
        _duration = _delay = 0;
    }

    public float Value(float now)
    {
        var elapsed = now - _start - _delay;
        if (elapsed <= 0) return _from;
        if (elapsed >= _duration) return _to;
        return _from + (_to - _from) * _curve.At(elapsed / _duration);
    }

    /// <summary>Whether the value is still moving (or waiting out its delay) at <paramref name="now" />.</summary>
    public bool Running(float now)
    {
        return now < _start + _delay + _duration;
    }
}
