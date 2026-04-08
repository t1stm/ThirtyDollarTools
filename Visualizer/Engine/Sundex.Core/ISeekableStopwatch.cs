using System.Diagnostics;

namespace Sundex.Core;

public interface ISeekableStopwatch
{
    public TimeSpan Elapsed { get; }
    public long ElapsedMilliseconds { get; }
    public bool IsRunning { get; }

    public void Start();
    public void Stop();
    public void Restart();
    public void Reset();
    public void Seek(long milliseconds);
}
