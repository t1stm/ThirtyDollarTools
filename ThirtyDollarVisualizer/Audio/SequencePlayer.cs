using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Audio.FeatureFlags;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Helpers.Timing;

namespace ThirtyDollarVisualizer.Audio;

public class SequencePlayer
{
    public readonly AudioContext AudioContext = new NullAudioContext();
    protected readonly SeekableStopwatch TimingStopwatch = new();
    protected readonly SemaphoreSlim UpdateLock = new(1);
    protected readonly Action<string>? Log;
    protected readonly Dictionary<string, Action<Placement, int>> EventActions = new();
    
    protected BufferHolder BufferHolder;
    protected TimedEvents Events;
    protected BackingAudio? BackingAudio;
    protected PlayerErrors Errors = PlayerErrors.None;
    public int PlacementIndex { get; private set; }
    protected readonly List<(string, AudibleBuffer)> ActiveSamples = new(256);
    protected readonly Greeting? Greeting;

    private bool _update_running;
    private bool _dead;
    private bool _cut_sounds;

    /// <summary>
    /// Creates a player that plays Thirty Dollar sequences.
    /// </summary>
    /// <param name="context">The audio context you want to use.</param>
    /// <param name="log_action">The logging action.</param>
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

    /// <summary>
    /// Subscribes a given event_name to a action, which is invoked when the event is played.
    /// </summary>
    /// <param name="event_name">The event's name.</param>
    /// <param name="action">The action you want to execute on encountering it.</param>
    public void SubscribeActionToEvent(string event_name, Action<Placement, int> action)
    {
        if (EventActions.TryAdd(event_name, action)) return;
        if (!EventActions.ContainsKey(event_name)) return;
        
        EventActions[event_name] = action;
    }

    /// <summary>
    /// Removes a subscribed action.
    /// </summary>
    /// <param name="event_name">The event's name you want to remove.</param>
    public void RemoveSubscription(string event_name)
    {
        EventActions.Remove(event_name);
    }

    /// <summary>
    /// Clears all subscriptions.
    /// </summary>
    public void ClearSubscriptions()
    {
        EventActions.Clear();
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
        if (!EventActions.TryGetValue(string.Empty, out var event_action))
        {
            UpdateLock.Release();
            return;
        }

        var placement = Events.Placement[PlacementIndex];
        await Task.Run(() => { event_action.Invoke(placement, (int)placement.SequenceIndex); })
            .ConfigureAwait(false);
        
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
        
        AlignToTime();
        UpdateLock.Release();
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
            await PlaybackUpdate();
            await Task.Delay(1);
        }
    }

    public void CutSounds()
    {
        lock (ActiveSamples)
        {
            foreach (var (_, buffer) in ActiveSamples)
            {
                buffer.Stop();
            }
        }
    }

    public void IndividualCutSamples(HashSet<string> cut_samples)
    {
        lock (ActiveSamples)
        {
            foreach (var (event_name, buffer) in ActiveSamples)
            {
                if (!cut_samples.Contains(event_name)) continue;
                buffer.Stop();
            }
        }
    }

    public void SetGreeting(GreetingType type)
    {
        if (Greeting == null) return;
        Greeting.GreetingType = type;
    }

    protected async Task PlaybackUpdate()
    {
        try
        {
            await UpdateLock.WaitAsync();
            var placement_memory = Events.Placement.AsMemory();
            var current_idx = PlacementIndex;
            int end_idx;

            var end_placement = placement_memory.Span[^1];
            var end_time = end_placement.Index;

            if (TimingStopwatch.ElapsedMilliseconds * Events.TimingSampleRate / 1000f + 1000 > end_time)
            {
                TimingStopwatch.Seek((long)(end_time * 1000f / Events.TimingSampleRate) + 100);
            }

            for (end_idx = current_idx; end_idx < placement_memory.Length; end_idx++)
            {
                var placement = placement_memory.Span[end_idx];
                if (placement.Index >
                    (ulong)((float)TimingStopwatch.ElapsedMilliseconds * Events.TimingSampleRate / 1000f))
                {
                    break;
                }

                if (placement.Event is IndividualCutEvent ice)
                {
                    IndividualCutSamples(ice.CutSounds);
                }
                
                switch (placement.Event.SoundEvent)
                {
                    case "!cut":
                        CutSounds();
                        break;
                }

                // Explicit pass. Checks whether there are events that need to execute differently from normal ones.
                if (EventActions.TryGetValue(placement.Event.SoundEvent ?? "", out var explicit_action))
                {
                    await Task.Run(() => { explicit_action.Invoke(placement, (int)placement.SequenceIndex); })
                        .ConfigureAwait(false);
                }


                // Normal pass.
                if (!EventActions.TryGetValue(string.Empty, out var event_action)) continue;

                await Task.Run(() => { event_action.Invoke(placement, (int)placement.SequenceIndex); })
                    .ConfigureAwait(false);
            }

            PlacementIndex = end_idx;

        
            var length = end_idx - current_idx;
            if (length < 1) return;

            PlayBatch(placement_memory.Span.Slice(current_idx, length));
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    protected void PlayBatch(Span<Placement> batch)
    {
        if (batch.Length < 1) return;
        
        if (AudioContext is IBatchSupported batch_supported)
        {
            var batch_span = new Span<AudibleBuffer>(new AudibleBuffer[batch.Length]);
            for (var i = 0; i < batch_span.Length; i++)
            {
                if (_cut_sounds)
                {
                    _cut_sounds = false;
                    return;
                }
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

            var name = placement.Event.SoundEvent ?? "";
            var tuple = (name, buffer);
            lock (ActiveSamples)
                ActiveSamples.Add(tuple);
            
            buffer.SetVolume((float) (placement.Event.Volume ?? 100d) / 100f);
            buffer.Play(remove_callback);
            continue;

            void remove_callback()
            {
                lock (ActiveSamples)
                    ActiveSamples.Remove(tuple);
            }
        }
    }

    ~SequencePlayer()
    {
        UpdateLock.Wait();

        ClearSubscriptions();
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
    public Sequence Sequence;
    public Placement[] Placement;
    public int TimingSampleRate;
}