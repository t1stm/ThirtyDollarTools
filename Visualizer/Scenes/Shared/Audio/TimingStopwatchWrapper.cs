using Shared.Audio.Features;
using Sundex.Core;

namespace Shared.Audio;

public class TimingStopwatchWrapper : ISeekableStopwatch
{
    public IBufferStopwatch? AudibleBuffer { get; set; }

    public TimeSpan Elapsed => AudibleBuffer?.Elapsed ?? TimeSpan.Zero;
    public long ElapsedMilliseconds => AudibleBuffer?.ElapsedMilliseconds ?? 0;
    public bool IsRunning => AudibleBuffer?.IsRunning ?? false;

    public void Start()
    {
        AudibleBuffer?.Start();
    }

    public void Stop()
    {
        AudibleBuffer?.Stop();
    }

    public void Restart()
    {
        AudibleBuffer?.Restart();
    }

    public void Reset()
    {
        AudibleBuffer?.Reset();
    }

    public void Seek(long milliseconds)
    {
        AudibleBuffer?.Seek(milliseconds);
    }
}