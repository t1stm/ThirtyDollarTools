using ThirtyDollarConverter.Objects;
using ThirtyDollarVisualizer.Audio.FeatureFlags;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Helpers.Timing;

namespace ThirtyDollarVisualizer.Audio;

public class SequencePlayer
{
    protected readonly SeekableStopwatch TimingStopwatch = new();
    protected readonly SemaphoreSlim UpdateLock = new(1);
    protected readonly AudioContext AudioContext = new NullAudioContext();
    protected readonly Action<string>? Log;

    protected BufferHolder BufferHolder;
    protected TimedEvents Events;
    protected BackingAudio? BackingAudio;
    protected PlayerErrors Errors = PlayerErrors.None;
    public int PlacementIndex { get; private set; }
    protected readonly List<AudibleBuffer> ActiveSamples = new(256);
    protected readonly Greeting? Greeting;

    private bool _update_running;
    private bool _dead;

    public SequencePlayer(AudioContext? context = null, Action<string>? log_action = null)
    {
        BufferHolder = new BufferHolder();
        Events = new TimedEvents
        {
            Placement = Array.Empty<Placement>(),
            TimingSampleRate = 100_000
        };
        Log = log_action;
        
        var c = context ?? GetAvailableContext();
        if (c == null)
        {
            Errors = PlayerErrors.NoContext;
            return;
        }
        
        AudioContext = c;
        Greeting = new Greeting(AudioContext, BufferHolder);
        TimingStopwatch.Reset();
    }

    protected AudioContext? GetAvailableContext()
    {
        AudioContext context;

        if ((context = new BassAudioContext()).Create()) return context;
        if ((context = new OpenALContext()).Create()) return context;

        Log?.Invoke("Unable to initialize the audio device.");
        return null;
    }

    public SeekableStopwatch GetTimingStopwatch() => TimingStopwatch;
    public AudioContext GetContext() => AudioContext;
    
    public async Task Restart()
    {
        await UpdateLock.WaitAsync();
        TimingStopwatch.Restart();
        AlignToTime();
        UpdateLock.Release();
    }

    public async Task RestartAfter(long milliseconds)
    {
        await UpdateLock.WaitAsync();
        TimingStopwatch.Restart();
        TimingStopwatch.Seek(-milliseconds);
        AlignToTime();
        UpdateLock.Release();
        await Start();
    }

    public async Task Seek(long milliseconds)
    {
        await UpdateLock.WaitAsync();
        TimingStopwatch.Seek(milliseconds);
        AlignToTime();
        UpdateLock.Release();
    }
    
    public async Task Start()
    {
        await (Greeting?.PlayWaitFinish() ?? Task.CompletedTask);
        TimingStopwatch.Restart();
        AlignToTime();
        UpdateLoop();
    }
    
    public async Task Stop()
    {
        await UpdateLock.WaitAsync();
        TimingStopwatch.Stop();
        UpdateLock.Release();
    }

    public void TogglePause()
    {
        switch (TimingStopwatch.IsRunning)
        {
            case true:
                TimingStopwatch.Stop();
                break;
            
            case false:
                TimingStopwatch.Start();
                break;
        }
    }

    public async Task UpdateSequence(BufferHolder holder, TimedEvents events)
    {
        await UpdateLock.WaitAsync();
        CutSounds();
        
        BufferHolder = holder;
        Events = events;
        UpdateLock.Release();

        await Stop();
        await Seek(0);
    }

    protected void AlignToTime()
    {
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var span = Events.Placement.AsSpan();
        var idx = PlacementIndex;
        var min_time = long.MaxValue;

        for (var i = 0; i < span.Length; i++)
        {
            var placement = span[i];
            
            var time = Math.Abs((long) placement.Index * 1000 / Events.TimingSampleRate - current_time);
            if (time >= min_time) continue;
            
            min_time = time;
            idx = i;
        }

        PlacementIndex = idx;
    }
    
    protected async void UpdateLoop()
    {
        if (_update_running) return;
        _update_running = true;
            
        while (_update_running && !_dead)
        {
            try
            {
                await UpdateLock.WaitAsync();
                PlaybackUpdate();

                await Task.Delay(1);
            }
            finally
            {
                UpdateLock.Release();
            }
        }
    }

    public void CutSounds()
    {
        lock (ActiveSamples)
        {
            foreach (var buffer in ActiveSamples)
            {
                buffer.Stop();
            }
        }
    }

    public void SetGreeting(GreetingType type)
    {
        if (Greeting == null) return;
        Greeting.GreetingType = type;
    }

    protected void PlaybackUpdate()
    {
        var placement_span = Events.Placement.AsSpan();
        var current_idx = PlacementIndex;
        int end_idx;
        
        var end_placement = placement_span[^1];
        var end_time = end_placement.Index;

        if (TimingStopwatch.ElapsedMilliseconds * Events.TimingSampleRate / 1000f + 1000 > end_time)
        {
            TimingStopwatch.Seek((long) (end_time * 1000f / Events.TimingSampleRate) + 100);
        }

        for (end_idx = current_idx; end_idx < placement_span.Length; end_idx++)
        {
            var placement = placement_span[end_idx];
            if (placement.Index > (ulong)((float)TimingStopwatch.ElapsedMilliseconds * Events.TimingSampleRate / 1000f))
            {
                break;
            }

            switch (placement.Event.SoundEvent)
            {
                case "!cut":
                    CutSounds();
                    continue;
                
                case "!divider":
                    //LastDividerIndex = placement.SequenceIndex;
                    continue;
            }
        }
        
        PlacementIndex = end_idx;

        var length = end_idx - current_idx;
        if (length < 1) return;
        
        PlayBatch(placement_span.Slice(current_idx, length));
    }

    protected void PlayBatch(Span<Placement> batch)
    {
        if (batch.Length < 1) return;
        
        if (AudioContext is IBatchSupported batch_supported)
        {
            var batch_span = new Span<AudibleBuffer>(new AudibleBuffer[batch.Length]);
            for (var i = 0; i < batch_span.Length; i++)
            {
                var el = batch[i];
                if (!el.Audible || !BufferHolder.TryGetBuffer(el.Event.SoundEvent ?? "", el.Event.Value, out var buffer))
                {
                    batch_span[i] = NullAudibleBuffer.EmptyBuffer;
                    continue;
                }
                
                batch_span[i] = buffer;
            }

            batch_supported.PlayBatch(batch_span);
            return;
        }

        foreach (var placement in batch)
        {
            if (!placement.Audible || 
                !BufferHolder.TryGetBuffer(placement.Event.SoundEvent ?? "", placement.Event.Value, out var buffer)) continue;
            
            lock (ActiveSamples)
                ActiveSamples.Add(buffer);
            
            buffer.SetVolume((float) (placement.Event.Volume ?? 100d) / 100f);
            buffer.Play(remove_callback);
            continue;

            void remove_callback()
            {
                lock (ActiveSamples)
                    ActiveSamples.Remove(buffer);
            }
        }
    }

    ~SequencePlayer()
    {
        UpdateLock.Wait();
        
        _update_running = false;
        _dead = true;
        
        UpdateLock.Release();
    }
}

public enum PlayerErrors
{
    None, 
    NoContext
}

public struct TimedEvents
{
    public Placement[] Placement;
    public int TimingSampleRate;
}