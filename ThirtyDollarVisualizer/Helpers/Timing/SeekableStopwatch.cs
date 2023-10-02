namespace ThirtyDollarVisualizer.Helpers.Timing;

public class SeekableStopwatch
{
    protected readonly SemaphoreSlim Lock = new(1);
    protected DateTime StartTime = DateTime.MinValue;
    protected DateTime? StopTime;
    protected TimeSpan LastValue = TimeSpan.Zero;

    protected bool Running;
    
    protected static DateTime GetCurrentTime()
    {
        return DateTime.Now;
    }

    public void Start()
    {
        if (Running) return;
        if (StartTime == DateTime.MinValue)
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
        StopTime = DateTime.Now;

        Lock.Release();
    }

    public void Seek(long delta)
    {
        Lock.Wait();
        
        var wanted_time = TimeSpan.FromMilliseconds(delta);
        LastValue = wanted_time;
        
        var current = GetCurrentTime();
        var delta_time = current - wanted_time;
        StartTime = delta_time;

        if (StopTime != null)
        {
            StopTime = current;
        }

        Lock.Release();
    }

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
            
            var val = LastValue = current_time - StartTime;
            
            Lock.Release();
            return val;
        }
    }

    public long ElapsedMilliseconds => (long) Elapsed.TotalMilliseconds;
    public bool IsRunning => Running;
}