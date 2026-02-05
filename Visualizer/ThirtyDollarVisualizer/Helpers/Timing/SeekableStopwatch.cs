using System.Diagnostics;

namespace ThirtyDollarVisualizer.Helpers.Timing;

public class SeekableStopwatch
{
    protected readonly SemaphoreSlim Lock = new(1);
    protected TimeSpan LastValue = TimeSpan.Zero;

    protected bool Running;
    protected long StartTime = long.MinValue;
    protected long? StopTime;

    public TimeSpan Elapsed
    {
        get
        {
            Lock.Wait();
            if (!Running)
            {
                Lock.Release();
                return LastValue;
            }

            var current_time = GetCurrentTime();

            if (StopTime != null)
            {
                StartTime += current_time - StopTime.Value;
                StopTime = null;
            }

            var val = LastValue = Stopwatch.GetElapsedTime(StartTime, current_time);

            Lock.Release();
            return val;
        }
    }

    public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;
    public bool IsRunning => Running;

    protected static long GetCurrentTime()
    {
        return Stopwatch.GetTimestamp();
    }

    public void Start()
    {
        if (Running) return;
        if (StartTime == long.MinValue)
            Restart();
        Running = true;
    }

    public void Restart()
    {
        Lock.Wait();

        Running = true;
        StopTime = null;
        StartTime = GetCurrentTime();

        Lock.Release();
    }

    public void Reset()
    {
        Lock.Wait();

        Running = false;
        StopTime = null;
        LastValue = TimeSpan.Zero;

        Lock.Release();
    }

    public void Stop()
    {
        if (!Running) return;

        Lock.Wait();

        Running = false;
        StopTime = Stopwatch.GetTimestamp();

        Lock.Release();
    }

    public void Seek(long delta)
    {
        Lock.Wait();

        var wanted_time = TimeSpan.FromMilliseconds(delta);
        LastValue = wanted_time;

        var current = GetCurrentTime();
        var delta_time = current - wanted_time.Ticks * Stopwatch.Frequency / 10000000;
        StartTime = delta_time;

        if (StopTime != null) StopTime = current;

        Lock.Release();
    }
}