using Serilog;
using Shared.Audio.BASS;
using Shared.Audio.Null;
using Shared.Audio.OpenAL;
using Shared.Objects;
using Sundex.Core;
using Sundex.Engine.Threading;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser.Custom_Events;
using ThirtyDollarConverter.Voices;

namespace Shared.Audio;

public class SequencePlayer
{
    public readonly AudioContext AudioContext = new NullAudioContext();
    protected readonly long[] Bookmarks = new long[10];
    protected readonly Dictionary<string, Action<Placement, int>> EventActions = new();
    protected readonly ILogger Logger;
    protected readonly SemaphoreSlim UpdateLock = new(1);

    protected PlayerErrors Errors = PlayerErrors.None;
    protected TimedEvents Events;

    protected SequenceIndices SequenceIndices = new();
    protected Action<int>? SequenceUpdateAction;

    /// <summary>The playing song's render-ahead, from its <see cref="UpdateSequence" />.</summary>
    private Func<double>? _aheadSeconds;

    private bool _dead;
    private long _oldTime;
    private bool _updateRunning;

    /// <summary>
    ///     Creates a player that plays Thirty Dollar sequences.
    /// </summary>
    /// <param name="logger">The Logger instance you want to give.</param>
    /// <param name="context">The audio context you want to use.</param>
    public SequencePlayer(ILogger logger, AudioContext? context = null)
    {
        Logger = logger.ForContext<SequencePlayer>();
        Events = new TimedEvents
        {
            Placement = [],
            TimingSampleRate = 100_000
        };

        var c = context;
        c?.Create();
        c ??= GetAvailableContext();

        if (c == null)
        {
            Errors = PlayerErrors.NoContext;
            return;
        }

        AudioContext = c;
    }

    public float Volume
    {
        get;
        protected set
        {
            field = value;
            Playback?.SetVolume(field);
        }
    } = 0.5f;

    protected TimingStopwatchWrapper TimingStopwatch { get; } = new();

    /// <summary>Streams the song; created by the first <see cref="UpdateSequence" />.</summary>
    protected VoicePlayback? Playback
    {
        get;
        set
        {
            field = value;
            TimingStopwatch.AudibleBuffer = field;
        }
    }

    protected int CurrentSequence
    {
        get;
        set
        {
            field = value;
            SequenceUpdateAction?.Invoke(field);
        }
    }

    public int PlacementIndex { get; private set; }

    /// <summary>The song mixer's CPU load - see <see cref="VoicePlayback.Load" />.</summary>
    public float CpuLoad => Playback?.Load ?? 0;

    protected AudioContext? GetAvailableContext()
    {
        AudioContext context;

        if ((context = new BassAudioContext(Logger)).Create() ||
            (context = new OpenALContext(Logger)).Create())
            return context;

        Logger.Error("Unable to initialize the audio device");
        return null;
    }

    /// <summary>
    ///     Subscribes a given event_name to a action, which is invoked when the event is played.
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
    ///     Subscribes an action that is called each time the sequence index is changed.
    /// </summary>
    /// <param name="action">The action to call.</param>
    public void SubscribeSequenceChange(Action<int>? action)
    {
        SequenceUpdateAction = action;
    }

    /// <summary>
    ///     Removes a subscribed action.
    /// </summary>
    /// <param name="eventName">The event's name you want to remove.</param>
    public void RemoveSubscription(string eventName)
    {
        var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();
        alternative_lookup.Remove(eventName);
    }

    /// <summary>
    ///     Clears all subscriptions.
    /// </summary>
    public void ClearSubscriptions()
    {
        EventActions.Clear();
    }

    public AudioContext GetContext()
    {
        return AudioContext;
    }

    public ISeekableStopwatch GetTimingStopwatch()
    {
        return TimingStopwatch;
    }

    public void Restart()
    {
        UpdateLock.Wait();
        try
        {
            Playback?.Restart();
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    public void SetVolume(float volume)
    {
        Volume = volume;
        Playback?.SetVolume(volume);
    }

    public void Seek(long milliseconds)
    {
        UpdateLock.Wait();
        try
        {
            Playback?.SeekTime_Milliseconds(milliseconds);

            var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alternative_lookup.TryGetValue(string.Empty, out var event_action)) return;
            if (Events.Placement.Length < 1) return;

            var index = Math.Clamp(PlacementIndex, 0, Events.Placement.Length - 1);
            var placement = Events.Placement[index];
            event_action.Invoke(placement, CurrentSequence);
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    public void Start(ThreadRunner threadRunner)
    {
        Playback?.Play();
        threadRunner.RunThread(UpdateLoop);
    }

    public void Stop()
    {
        UpdateLock.Wait();
        try
        {
            Playback?.Stop();
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    public void TogglePause()
    {
        Playback?.SetPause(Playback.IsRunning);
    }

    /// <summary>
    ///     Switches to a new song, or a new version of it: <paramref name="voices" /> takes over
    ///     at the playhead (callers that want it from the start stop and start the player around
    ///     this). Takes over one reference to <paramref name="voices" /> - see
    ///     <see cref="VoicePlayback.Publish" />.
    /// </summary>
    /// <param name="aheadSeconds">
    ///     How far ahead of the playhead the song is mixed - the owner's choice, read again on
    ///     every <see cref="RefreshAheadSeconds" /> until someone else uploads.
    /// </param>
    public void UpdateSequence(TimedEvents events, SequenceIndices sequenceIndices, VoiceList voices,
        Func<double> aheadSeconds)
    {
        UpdateLock.Wait();
        try
        {
            Events = events;
            SequenceIndices = sequenceIndices;
            _aheadSeconds = aheadSeconds;

            Playback ??= new VoicePlayback(AudioContext, AudioContext.SampleRate, aheadSeconds());
            Playback.AheadSeconds = aheadSeconds();
            Playback.Publish(voices);
            Playback.SetVolume(Volume);
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    /// <summary>
    ///     Re-reads the render-ahead of whoever uploaded the playing song, for a setting moved
    ///     mid-song: a longer one tops the queue up on the feeder's next pass, a shorter one lets
    ///     it drain down.
    /// </summary>
    public void RefreshAheadSeconds()
    {
        UpdateLock.Wait();
        try
        {
            if (Playback != null && _aheadSeconds != null) Playback.AheadSeconds = _aheadSeconds();
        }
        finally
        {
            UpdateLock.Release();
        }
    }

    public void AlignToTime()
    {
        var current_time = Playback?.GetTime_Milliseconds() ?? 0;
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
            UpdateLock.Wait();
            try
            {
                PlaybackUpdate();
            }
            finally
            {
                UpdateLock.Release();
            }

            Thread.Sleep(1);
        }
    }

    public long SeekToBookmark(int bookmarkIndex)
    {
        long bookmark_time;

        lock (Bookmarks)
        {
            bookmark_time = Bookmarks[bookmarkIndex];
        }

        Seek(bookmark_time);
        return bookmark_time;
    }

    public long SetBookmark(int bookmarkIndex)
    {
        var current_time = Playback?.GetTime_Milliseconds() ?? 0;
        SetBookmarkTo(bookmarkIndex, current_time);
        return current_time;
    }

    public void SetBookmarkTo(int bookmarkIndex, long milliseconds)
    {
        lock (Bookmarks)
        {
            Bookmarks[bookmarkIndex] = milliseconds;
        }
    }

    public void ClearBookmark(int bookmarkIndex)
    {
        lock (Bookmarks)
        {
            Bookmarks[bookmarkIndex] = 0;
        }
    }

    protected void PlaybackUpdate()
    {
        var placement_memory = Events.Placement.AsMemory();

        var end_placement = placement_memory.Span[^1];
        var end_time = end_placement.Index;

        var alternative_lookup = EventActions.GetAlternateLookup<ReadOnlySpan<char>>();
        var currentTime = Playback?.GetTime_Milliseconds() ?? 0;

        if (GetIndexFromTime(currentTime) + 1000 > end_time)
            currentTime = GetTimeFromIndex(end_time) + 100;

        if (_oldTime > currentTime)
            AlignToTime();

        _oldTime = currentTime;

        var current_idx = PlacementIndex;
        int end_idx;
        for (end_idx = current_idx; end_idx < placement_memory.Length; end_idx++)
        {
            var placement = placement_memory.Span[end_idx];
            if (placement.Index > GetIndexFromTime(currentTime))
                break;

            if (placement.Event is EndEvent)
            {
                CurrentSequence = SequenceIndices.GetSequenceIDFromIndex(placement.Index);
                continue;
            }

            // Explicit pass. Checks whether there are events that need to execute differently from normal ones.
            if (alternative_lookup.TryGetValue(placement.Event.SoundEvent ?? "", out var explicit_action))
                explicit_action.Invoke(placement, CurrentSequence);

            // Normal pass.
            if (!alternative_lookup.TryGetValue(string.Empty, out var event_action)) continue;
            event_action.Invoke(placement, CurrentSequence);
        }

        PlacementIndex = end_idx;
    }

    /// <summary>
    ///     Gets the time in milliseconds a timing index represents.
    /// </summary>
    /// <param name="index">The sequence's index.</param>
    /// <returns>index * 1000 / Events.TimingSampleRate</returns>
    public long GetTimeFromIndex(ulong index)
    {
        return (long)index * 1000 / Events.TimingSampleRate;
    }

    /// <summary>
    ///     Gets the sequence's timing index from the given milliseconds.
    /// </summary>
    /// <param name="milliseconds">The milliseconds for the index you want to find.</param>
    /// <returns>milliseconds * Events.TimingSampleRate / 1000f</returns>
    public ulong GetIndexFromTime(long milliseconds)
    {
        return (ulong)(milliseconds * Events.TimingSampleRate / 1000f);
    }

    public void Die()
    {
        UpdateLock.Wait();
        try
        {
            ClearSubscriptions();
            _updateRunning = false;
            _dead = true;
        }
        finally
        {
            UpdateLock.Release();
        }
    }


    ~SequencePlayer()
    {
        Die();
    }
}