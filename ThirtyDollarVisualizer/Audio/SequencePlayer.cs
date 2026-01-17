using ThirtyDollarConverter.Objects;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Audio.BASS;
using ThirtyDollarVisualizer.Audio.Features;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Audio.OpenAL;
using ThirtyDollarVisualizer.Helpers.Timing;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Audio;

public class SequencePlayer
{
    private static int InstanceID;
    
    protected readonly List<(string, AudibleBuffer)> ActiveSamples = new(256);
    public readonly AudioContext AudioContext = new NullAudioContext();
    protected readonly long[] Bookmarks = new long[10];
    protected readonly Dictionary<string, Action<Placement, int>> EventActions = new();
    protected readonly Greeting? Greeting;
    protected readonly Action<string>? Log;
    protected readonly SeekableStopwatch TimingStopwatch = new();
    protected readonly SemaphoreSlim UpdateLock = new(1);
    private int _currentSequence;
    private bool _cutSounds;
    private bool _dead;

    private bool _updateRunning;
    protected BackingAudio? BackingAudio;

    protected BufferHolder BufferHolder;
    protected PlayerErrors Errors = PlayerErrors.None;
    protected TimedEvents Events;

    protected SequenceIndices SequenceIndices = new();
    protected Action<int>? SequenceUpdateAction;

    /// <summary>
    /// Creates a player that plays Thirty Dollar sequences.
    /// </summary>
    /// <param name="context">The audio context you want to use.</param>
    /// <param name="logAction">The logging action.</param>
    public SequencePlayer(AudioContext? context = null, Action<string>? logAction = null)
    {
        ++InstanceID;
        BufferHolder = new BufferHolder();
        Events = new TimedEvents
        {
            Placement = [],
            TimingSampleRate = 100_000
        };
        Log = logAction;

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

    protected int CurrentSequence
    {
        get => _currentSequence;
        set
        {
            _currentSequence = value;
            SequenceUpdateAction?.Invoke(_currentSequence);
        }
    }

    public int PlacementIndex { get; private set; }

    protected AudioContext? GetAvailableContext()
    {
        AudioContext context;

        if ((context = new BassAudioContext()).Create() ||
            (context = new OpenALContext()).Create())
            return context;

        Log?.Invoke("Unable to initialize the audio device.");
        return null;
    }

    /// <summary>
    /// Subscribes a given event_name to a action, which is invoked when the event is played.
    /// </summary>
    /// <param name="eventName">The event's name.</param>
    /// <param name="action">The action you want to execute on encountering it.</param>
    public void SubscribeActionToEvent(string eventName, Action<Placement, int> action)
    {
        var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();
        if (alternative_lookup.TryAdd(eventName, action)) return;
        if (!alternative_lookup.ContainsKey(eventName)) return;

        alternative_lookup[eventName] = action;
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
    /// Removes a subscribed action.
    /// </summary>
    /// <param name="eventName">The event's name you want to remove.</param>
    public void RemoveSubscription(string eventName)
    {
        var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();
        alternative_lookup.Remove(eventName);
    }

    /// <summary>
    /// Clears all subscriptions.
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

    public async ValueTask Restart()
    {
        await UpdateLock.WaitAsync();
        TimingStopwatch.Restart();
        AlignToTime();
        UpdateLock.Release();
    }

    public async ValueTask RestartAfter(long milliseconds)
    {
        await UpdateLock.WaitAsync();
        TimingStopwatch.Restart();
        TimingStopwatch.Seek(-milliseconds);
        AlignToTime();
        UpdateLock.Release();
        await Start();
    }

    public void Seek(long milliseconds)
    {
        UpdateLock.Wait();
        TimingStopwatch.Seek(milliseconds);
        AlignToTime();

        var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();
        if (!alternative_lookup.TryGetValue(string.Empty, out var event_action))
        {
            UpdateLock.Release();
            return;
        }

        var placement = Events.Placement[PlacementIndex];
        event_action.Invoke(placement, CurrentSequence);

        UpdateLock.Release();
    }

    public async ValueTask Start()
    {
        await (Greeting?.PlayWaitFinish() ?? Task.CompletedTask);
        TimingStopwatch.Restart();
        AlignToTime();
        // Spawns a new thread object for the Thread.Sleep in the update loop.
        new Thread(UpdateLoop).Start();
    }

    public async ValueTask Stop()
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

    public async ValueTask UpdateSequence(BufferHolder holder, TimedEvents events, SequenceIndices sequenceIndices)
    {
        await UpdateLock.WaitAsync();
        CutSounds();

        BufferHolder = holder;
        Events = events;
        SequenceIndices = sequenceIndices;
        CurrentSequence = 0;

        AlignToTime();
        UpdateLock.Release();
    }

    protected void AlignToTime()
    {
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var span = Events.Placement.AsSpan();
        if (span.Length < 1) return;

        var idx = PlacementIndex;
        var min_time = long.MaxValue;

        Placement? placement;
        for (var i = 0; i < span.Length; i++)
        {
            placement = span[i];
            var index_time = GetTimeFromIndex(placement.Index);
            var time = Math.Abs(index_time - current_time);
            if (time >= min_time) continue;

            min_time = time;
            idx = i;
        }

        PlacementIndex = idx;
        placement = span[idx];
        CurrentSequence = SequenceIndices.GetSequenceIDFromIndex(placement.Index);
    }

    protected void UpdateLoop()
    {
        if (_updateRunning) return;
        _updateRunning = true;

        while (_updateRunning && !_dead)
        {
            PlaybackUpdate();
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

    public void IndividualCutSamples(HashSet<string> cutSamples)
    {
        var alternative_lookup = cutSamples.GetAlternateLookup<ReadOnlySpan<char>>();

        lock (ActiveSamples)
        {
            foreach (var (event_name, buffer) in ActiveSamples)
            {
                if (!alternative_lookup.Contains(event_name)) continue;
                buffer.Stop();
            }
        }
    }

    public void SetGreeting(GreetingType type)
    {
        if (Greeting == null) return;
        Greeting.GreetingType = type;
    }

    public long SeekToBookmark(int bookmarkIndex)
    {
        var bookmark_time = Bookmarks[bookmarkIndex];

        Seek(bookmark_time);
        return bookmark_time;
    }

    public long SetBookmark(int bookmarkIndex)
    {
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        SetBookmarkTo(bookmarkIndex, current_time);
        return current_time;
    }

    public void SetBookmarkTo(int bookmarkIndex, long milliseconds)
    {
        Bookmarks[bookmarkIndex] = milliseconds;
    }

    public void ClearBookmark(int bookmarkIndex)
    {
        Bookmarks[bookmarkIndex] = 0;
    }

    protected void PlaybackUpdate()
    {
        try
        {
            UpdateLock.Wait();
            var placement_memory = Events.Placement.AsMemory();
            var current_idx = PlacementIndex;
            int end_idx;

            var end_placement = placement_memory.Span[^1];
            var end_time = end_placement.Index;

            var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();

            if (GetIndexFromTime(TimingStopwatch.ElapsedMilliseconds) + 1000 > end_time)
                TimingStopwatch.Seek(GetTimeFromIndex(end_time) + 100);

            for (end_idx = current_idx; end_idx < placement_memory.Length; end_idx++)
            {
                var placement = placement_memory.Span[end_idx];
                if (placement.Index > GetIndexFromTime(TimingStopwatch.ElapsedMilliseconds))
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
                if (alternative_lookup.TryGetValue(placement.Event.SoundEvent ?? "", out var explicit_action))
                    explicit_action.Invoke(placement, CurrentSequence);

                // Normal pass.
                if (!alternative_lookup.TryGetValue(string.Empty, out var event_action)) continue;
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

    /// <summary>
    /// Gets the time in milliseconds a timing index represents.
    /// </summary>
    /// <param name="index">The sequence's index.</param>
    /// <returns>index * 1000 / Events.TimingSampleRate</returns>
    public long GetTimeFromIndex(ulong index)
    {
        return (long)index * 1000 / Events.TimingSampleRate;
    }

    /// <summary>
    /// Gets the sequence's timing index from the given milliseconds.
    /// </summary>
    /// <param name="milliseconds">The milliseconds for the index you want to find.</param>
    /// <returns>milliseconds * Events.TimingSampleRate / 1000f</returns>
    public ulong GetIndexFromTime(long milliseconds)
    {
        return (ulong)(milliseconds * Events.TimingSampleRate / 1000f);
    }

    protected void PlayBatch(Span<Placement> batch)
    {
        if (batch.Length < 1) return;

        if (AudioContext is IBatchSupported batch_supported)
        {
            var batch_span = new Span<AudibleBuffer>(new AudibleBuffer[batch.Length]);
            for (var i = 0; i < batch_span.Length; i++)
            {
                if (_cutSounds)
                {
                    _cutSounds = false;
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
            buffer.Play(RemoveCallback);
            continue;

            void RemoveCallback()
            {
                lock (ActiveSamples)
                {
                    ActiveSamples.Remove(tuple);
                }
            }
        }
    }

    /// <summary>
    /// Signals to the SequencePlayer to stop all execution and free all busy threads.
    /// </summary>
    public void Die()
    {
        UpdateLock.Wait();

        ClearSubscriptions();
        _updateRunning = false;
        _dead = true;

        UpdateLock.Release();
    }

    ~SequencePlayer()
    {
        Die();
    }
}

public enum PlayerErrors
{
    None,
    NoContext
}