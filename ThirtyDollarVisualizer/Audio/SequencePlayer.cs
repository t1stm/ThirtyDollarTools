using ThirtyDollarConverter.Objects;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Audio.FeatureFlags;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Helpers.Timing;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Audio;

public class SequencePlayer
{
    public readonly AudioContext AudioContext = new NullAudioContext();
    protected readonly List<(string, AudibleBuffer)> ActiveSamples = new(256);
    protected readonly long[] Bookmarks = new long[10];
    protected readonly Dictionary<string, Action<Placement, int>> EventActions = new();
    protected readonly Greeting? Greeting;
    protected readonly Action<string>? Log;
    protected readonly SeekableStopwatch TimingStopwatch = new();
    protected readonly SemaphoreSlim UpdateLock = new(1);
    private bool _cut_sounds;
    private bool _dead;

    private bool _update_running;
    protected BackingAudio? BackingAudio;

    protected BufferHolder BufferHolder;
    protected PlayerErrors Errors = PlayerErrors.None;
    protected TimedEvents Events;
    protected Action<int>? SequenceUpdateAction;
    
    protected SequenceIndices SequenceIndices = new();
    private int _current_sequence;
    protected int CurrentSequence
    {
        get => _current_sequence;
        set
        {
            _current_sequence = value;
            SequenceUpdateAction?.Invoke(_current_sequence);
        }
    }

    /// <summary>
    ///     Creates a player that plays Thirty Dollar sequences.
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

        var c = context;
        c?.Create();
        c ??= GetAvailableContext();

        if (c == null)
        {
            Errors = PlayerErrors.NoContext;
            return;
        }

        AudioContext = c;
        Greeting = new Greeting(AudioContext, BufferHolder);
        TimingStopwatch.Reset();
    }

    public int PlacementIndex { get; private set; }

    protected AudioContext? GetAvailableContext()
    {
        AudioContext context;

        if ((context = new BassAudioContext()).Create()) return context;
        if ((context = new OpenALContext()).Create()) return context;

        Log?.Invoke("Unable to initialize the audio device.");
        return null;
    }

    /// <summary>
    ///     Subscribes a given event_name to a action, which is invoked when the event is played.
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
    /// Subscribes an action that is called each time the sequence index is changed.
    /// </summary>
    /// <param name="action">The action to call.</param>
    public void SubscribeSequenceChange(Action<int>? action)
    {
        SequenceUpdateAction = action;
    }

    /// <summary>
    ///     Removes a subscribed action.
    /// </summary>
    /// <param name="event_name">The event's name you want to remove.</param>
    public void RemoveSubscription(string event_name)
    {
        EventActions.Remove(event_name);
    }

    /// <summary>
    ///     Clears all subscriptions.
    /// </summary>
    public void ClearSubscriptions()
    {
        EventActions.Clear();
    }

    public SeekableStopwatch GetTimingStopwatch()
    {
        return TimingStopwatch;
    }

    public AudioContext GetContext()
    {
        return AudioContext;
    }

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
        await Task.Run(() => { event_action.Invoke(placement, CurrentSequence); })
            .ConfigureAwait(false);

        UpdateLock.Release();
    }

    public async Task Start()
    {
        await (Greeting?.PlayWaitFinish() ?? Task.CompletedTask);
        TimingStopwatch.Restart();
        AlignToTime();
        // Spawns a new thread object for the Thread.Sleep in the update loop.
        new Thread(UpdateLoop).Start();
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

    public async Task UpdateSequence(BufferHolder holder, TimedEvents events, SequenceIndices sequence_indices)
    {
        await UpdateLock.WaitAsync();
        CutSounds();

        BufferHolder = holder;
        Events = events;
        SequenceIndices = sequence_indices;
        CurrentSequence = 0;

        AlignToTime();
        UpdateLock.Release();
    }

    protected void AlignToTime()
    {
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var span = Events.Placement.AsSpan();
        var idx = PlacementIndex;
        var min_time = long.MaxValue;

        Placement? placement;
        for (var i = 0; i < span.Length; i++)
        {
            placement = span[i];
            var index_time = placement.Index * 1000f / Events.TimingSampleRate;
            var time = Math.Abs((long)index_time - current_time);
            if (time >= min_time) continue;

            min_time = time;
            idx = i;
        }

        PlacementIndex = idx;
        placement = span[idx];
        CurrentSequence = SequenceIndices.GetSequenceIDFromIndex(placement.Index);
    }

    protected async void UpdateLoop()
    {
        if (_update_running) return;
        _update_running = true;

        while (_update_running && !_dead)
        {
            await PlaybackUpdate();
            // Using Thread.Sleep since it doesn't allocate memory.
            Thread.Sleep(1);
        }
    }

    public void CutSounds()
    {
        lock (ActiveSamples)
        {
            foreach (var (_, buffer) in ActiveSamples) buffer.Stop();
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

    public async Task<long> SeekToBookmark(int bookmark_index)
    {
        var bookmark_time = Bookmarks[bookmark_index];

        await Seek(bookmark_time);
        return bookmark_time;
    }

    public long SetBookmark(int bookmark_index)
    {
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        SetBookmarkTo(bookmark_index, current_time);
        return current_time;
    }

    public void SetBookmarkTo(int bookmark_index, long milliseconds)
    {
        Bookmarks[bookmark_index] = milliseconds;
    }

    public void ClearBookmark(int bookmark_index)
    {
        Bookmarks[bookmark_index] = 0;
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
                TimingStopwatch.Seek((long)(end_time * 1000f / Events.TimingSampleRate) + 100);

            for (end_idx = current_idx; end_idx < placement_memory.Length; end_idx++)
            {
                var placement = placement_memory.Span[end_idx];
                if (placement.Index >
                    (ulong)((float)TimingStopwatch.ElapsedMilliseconds * Events.TimingSampleRate / 1000f))
                    break;

                switch (placement.Event)
                {
                    case IndividualCutEvent ice:
                        IndividualCutSamples(ice.CutSounds);
                        break;
                    
                    case EndEvent:
                        CurrentSequence = SequenceIndices.GetSequenceIDFromIndex(placement.Index);
                        continue;
                }

                switch (placement.Event.SoundEvent)
                {
                    case "!cut":
                        CutSounds();
                        break;
                }

                // Explicit pass. Checks whether there are events that need to execute differently from normal ones.
                if (EventActions.TryGetValue(placement.Event.SoundEvent ?? "", out var explicit_action))
                    explicit_action.Invoke(placement, CurrentSequence);

                // Normal pass.
                if (!EventActions.TryGetValue(string.Empty, out var event_action)) continue;
                event_action.Invoke(placement, CurrentSequence);
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
                if (!el.Audible ||
                    !BufferHolder.TryGetBuffer(el.Event.SoundEvent ?? "", el.Event.Value, out var buffer))
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
                !BufferHolder.TryGetBuffer(placement.Event.SoundEvent ?? "", placement.Event.Value, out var buffer))
                continue;

            var name = placement.Event.SoundEvent ?? "";
            var tuple = (name, buffer);
            lock (ActiveSamples)
            {
                ActiveSamples.Add(tuple);
            }

            if (placement.Event is PannedEvent panned_event)
                buffer.SetPan(panned_event.Pan);
            else
                buffer.SetPan(0f);

            buffer.SetVolume((float)placement.Event.WorkingVolume / 100f);
            buffer.Play(remove_callback);
            continue;

            void remove_callback()
            {
                lock (ActiveSamples)
                {
                    ActiveSamples.Remove(tuple);
                }
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