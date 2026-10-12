using OpenTK.Graphics.OpenGL;

namespace Sundex.Engine.Renderer.Debug;

/// <summary>
///     Measures how long the GPU spends on the draws between <see cref="Begin" /> and
///     <see cref="End" />, one frame at a time, with a small ring of <c>GL_TIME_ELAPSED</c>
///     queries. A result is read only once the driver says it is available, a few frames
///     later, so measuring never stalls the pipeline. A frame that finds every query still
///     in flight goes unmeasured rather than waiting.
/// </summary>
public sealed class GpuTimer : IDisposable
{
    private readonly int[] _queries = new int[4];
    private readonly bool[] _pending = new bool[4];
    private int _next;
    private bool _running;

    public GpuTimer()
    {
        for (var i = 0; i < _queries.Length; i++) _queries[i] = GL.GenQuery();
    }

    /// <summary>GPU time of each measured frame in milliseconds, in the order they came back.</summary>
    public List<double> Samples { get; } = [];

    public void Begin()
    {
        Collect();
        if (_pending[_next]) return;

        GL.BeginQuery(QueryTarget.TimeElapsed, _queries[_next]);
        _running = true;
    }

    public void End()
    {
        if (!_running) return;

        GL.EndQuery(QueryTarget.TimeElapsed);
        _pending[_next] = true;
        _next = (_next + 1) % _queries.Length;
        _running = false;
    }

    /// <summary>Moves every finished query's result into <see cref="Samples" />.</summary>
    public void Collect()
    {
        for (var i = 0; i < _queries.Length; i++)
        {
            if (!_pending[i]) continue;

            GL.GetQueryObjecti(_queries[i], QueryObjectParameterName.QueryResultAvailable, out var available);
            if (available == 0) continue;

            GL.GetQueryObjecti64(_queries[i], QueryObjectParameterName.QueryResult, out var nanoseconds);
            Samples.Add(nanoseconds / 1_000_000.0);
            _pending[i] = false;
        }
    }

    /// <summary>
    ///     The nearest-rank percentile of <paramref name="samples" />: the smallest sample
    ///     that at least <paramref name="percent" />% of them are less than or equal to.
    ///     The input is left unsorted.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> samples, double percent)
    {
        if (samples.Count == 0) throw new ArgumentException("No samples.", nameof(samples));

        var sorted = samples.Order().ToArray();
        var rank = (int)Math.Ceiling(percent / 100 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    public void Dispose()
    {
        foreach (var query in _queries) GL.DeleteQuery(query);
    }
}
