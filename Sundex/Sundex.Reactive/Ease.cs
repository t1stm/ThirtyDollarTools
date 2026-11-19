namespace Sundex.Reactive;

/// <summary>Easing curves and a tween on the frame tick, ended with the current scope.</summary>
public static class Ease
{
    public static double Linear(double progress) => progress;

    public static double Out(double progress) => 1 - Math.Pow(1 - progress, 3);

    public static double InOut(double progress) => progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.Pow(-2 * progress + 2, 3) / 2;

    /// <summary>CSS <c>cubic-bezier(x1, y1, x2, y2)</c>.</summary>
    public static Func<double, double> Bezier(double x1, double y1, double x2, double y2) =>
        x => Solve(x1, y1, x2, y2, x);

    /// <summary>Runs <paramref name="apply" /> each frame with the eased progress, 0 to 1, over <paramref name="milliseconds" />.</summary>
    public static IDisposable To(double milliseconds, Action<double> apply, Action? done = null, Func<double, double>? curve = null)
    {
        curve ??= Out;
        var start = Frame.Now;
        var cancelled = new Cancel();
        Scope.Current?.OnDispose(cancelled.Dispose);
        apply(0);
        Frame.Tick(() =>
        {
            if (cancelled.Done) return false;
            var progress = milliseconds <= 0 ? 1 : Math.Clamp((Frame.Now - start) / milliseconds, 0, 1);
            apply(curve(progress));
            if (progress < 1) return true;
            done?.Invoke();
            return false;
        });
        return cancelled;
    }

    private static double Solve(double x1, double y1, double x2, double y2, double x)
    {
        double low = 0, high = 1, guess = x;
        for (var iteration = 0; iteration < 16; iteration++)
        {
            var at = Axis(x1, x2, guess);
            if (Math.Abs(at - x) < 1e-4) break;
            if (at < x) low = guess;
            else high = guess;
            guess = (low + high) / 2;
        }

        return Axis(y1, y2, guess);
    }

    private static double Axis(double first, double second, double progress) =>
        3 * (1 - progress) * (1 - progress) * progress * first + 3 * (1 - progress) * progress * progress * second + progress * progress * progress;

    private sealed class Cancel : IDisposable
    {
        public bool Done;
        public void Dispose() => Done = true;
    }
}
