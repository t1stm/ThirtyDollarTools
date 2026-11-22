using Sundex.Core;

namespace Shared.Audio;

public class TimingStopwatchWrapper : ISeekableStopwatch
{
    /// <summary>The playing song's clock, once there is one.</summary>
    public ISeekableStopwatch? Source { get; set; }

    public TimeSpan Elapsed => Source?.Elapsed ?? TimeSpan.Zero;
    public long ElapsedMilliseconds => Source?.ElapsedMilliseconds ?? 0;
    public bool IsRunning => Source?.IsRunning ?? false;

    public void Start()
    {
        Source?.Start();
    }

    public void Stop()
    {
        Source?.Stop();
    }

    public void Restart()
    {
        Source?.Restart();
    }

    public void Reset()
    {
        Source?.Reset();
    }

    public void Seek(long milliseconds)
    {
        Source?.Seek(milliseconds);
    }
}