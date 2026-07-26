using System;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Objects;

public class Placement : IEquatable<Placement>
{
    /// <summary>
    ///     Whether the event is processed by the PCM Encoder.
    /// </summary>
    public bool Audible { get; init; } = true;

    /// <summary>
    ///     The event this placement holds.
    /// </summary>
    public BaseEvent Event { get; init; } = NormalEvent.Empty;

    /// <summary>
    ///     The placement's index, audio-wise.
    /// </summary>
    public ulong Index { get; init; }

    /// <summary>
    ///     The placement's index, sequence-wise. Used in the Thirty Dollar Visualizer.
    /// </summary>
    public ulong SequenceIndex { get; init; }

    public bool Equals(Placement? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Audible == other.Audible && Index == other.Index &&
               Event.SoundEvent == other.Event.SoundEvent &&
               !Different(Event.Value, other.Event.Value) &&
               !Different(Event.WorkingValue, other.Event.WorkingValue) &&
               !Different(Event.Volume ?? 100, other.Event.Volume ?? 100) &&
               !Different(Event.WorkingVolume, other.Event.WorkingVolume) &&
               !Different((Event as ExtendedEvent)?.Pan, (other.Event as ExtendedEvent)?.Pan) &&
               !Different((Event as ExtendedEvent)?.OffsetInSeconds, (other.Event as ExtendedEvent)?.OffsetInSeconds) &&
               SameCutSounds(Event, other.Event);
    }

    /// <summary>
    ///     A cut is only the same cut when it silences the same sounds. Nothing else here
    ///     distinguishes two <see cref="IndividualCutEvent" />s, so without this an
    ///     instrument losing a sound leaves its cuts looking untouched - they stay out of
    ///     the incremental diff, and the dropped sound gets subtracted uncut from a mix that
    ///     had it cut, ringing on as if the cuts had been undone.
    /// </summary>
    private static bool SameCutSounds(BaseEvent a, BaseEvent b)
    {
        if (a is not IndividualCutEvent cut_a) return b is not IndividualCutEvent;
        return b is IndividualCutEvent cut_b && cut_a.CutSounds.SetEquals(cut_b.CutSounds);
    }

    private static bool Different(double? a, double? b)
    {
        if (a is null && b is null) return false;
        if (a is null || b is null) return true;
        return Math.Abs(a.Value - b.Value) > double.Epsilon;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Placement)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Audible, Index, Event.Value, Event.SoundEvent, Event.Volume ?? 100);
    }
}