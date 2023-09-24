namespace ThirtyDollarVisualizer.Helpers.Timing;

public class SeekableStopwatch
{
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
        if (StartTime == DateTime.MinValue)
            Restart();
        Running = true;
    }

    public void Restart()
    {
        Running = true;
        StopTime = null;
        StartTime = GetCurrentTime();
    }

    public void Reset()
    {
        Running = false;
        StopTime = null;
        LastValue = TimeSpan.Zero;
    }

    public void Stop()
    {
        if (!Running) return;
        
        Running = false;
        StopTime = DateTime.Now;
    }

    public void Seek(long delta)
    {
        StopTime = null;
        var wanted_time = TimeSpan.FromMilliseconds(delta);
        LastValue = wanted_time;
        
        var current = GetCurrentTime();
        var delta_time = current - wanted_time;
        StartTime = delta_time;
    }

    public TimeSpan Elapsed
    {
        get
        {
            if (!Running) return LastValue;

            var current_time = GetCurrentTime();
            
            if (StopTime != null)
            {
                StartTime += current_time - StopTime.Value;
                StopTime = null;
            }
            
            LastValue = current_time - StartTime;
        
            return LastValue;
        }
    }

    public long ElapsedMilliseconds => (long) Elapsed.TotalMilliseconds;
    public bool IsRunning => Running;
}