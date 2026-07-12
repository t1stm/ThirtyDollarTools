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
               !Different((Event as ExtendedEvent)?.OffsetInSeconds, (other.Event as ExtendedEvent)?.OffsetInSeconds);
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