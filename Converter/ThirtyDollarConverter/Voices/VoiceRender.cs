using System;
using System.Collections.Generic;
using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.Voices;

/// <summary>
///     A render that hasn't been mixed: its placement, its voices, and the resampled sounds a
///     rebuild can reuse. Disposing it releases its reference to <see cref="Voices" />.
/// </summary>
public sealed class VoiceRender : IDisposable
{
    public required TimedEvents TimedEvents { get; init; }
    public required VoiceList Voices { get; init; }
    public required Dictionary<(string, double), ProcessedEvent> ProcessedEvents { get; init; }
    public required uint SampleRate { get; init; }

    public void Dispose()
    {
        Voices.Dispose();
    }
}
