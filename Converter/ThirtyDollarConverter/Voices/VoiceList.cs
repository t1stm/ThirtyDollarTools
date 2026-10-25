using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ThirtyDollarConverter.Encoder.Mixers;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Voices;

/// <summary>
///     Every audible sound of a render as a <see cref="Voice" />, sorted by start, with identical
///     sounds stacked on one another folded into one. Mixing any
///     range of the song only reads this list, so playback can stream it a block at a time
///     (<see cref="VoiceCursor" />) and an offline render can mix slices of it in parallel
///     (<see cref="VoiceMixer" />), and both produce the same floats.
///     <para>
///         The arrays are rented from <see cref="ArrayPool{T}.Shared" /> and go back on the last
///         <see cref="Dispose" />. A list starts with one reference; whoever hands it to a second
///         owner (a playback that keeps reading it) calls <see cref="Retain" /> first, and each
///         owner disposes it once when it is done reading.
///     </para>
/// </summary>
public sealed class VoiceList : IDisposable
{
    private int _references = 1;
    private AudioData<float>[] _sounds;
    private int[] _starts;
    private Voice[] _voices;

    private VoiceList(Voice[] voices, int[] starts, AudioData<float>[] sounds, int count, int length, int longest,
        int channels, int fadeLength)
    {
        _voices = voices;
        _starts = starts;
        _sounds = sounds;
        Count = count;
        Length = length;
        Longest = longest;
        Channels = channels;
        FadeLength = fadeLength;
    }

    public int Count { get; }

    /// <summary>The song's length in samples - what a full render's buffer is sized to.</summary>
    public int Length { get; }

    /// <summary>The longest <c>End - Start</c> of any voice: how far back a range has to look for voices still sounding.</summary>
    public int Longest { get; }

    public int Channels { get; }

    /// <summary>Samples a cut voice fades over.</summary>
    public int FadeLength { get; }

    internal ReadOnlySpan<Voice> Voices => _voices.AsSpan(0, Count);
    internal ReadOnlySpan<int> Starts => _starts.AsSpan(0, Count);

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _references) != 0) return;

        ArrayPool<Voice>.Shared.Return(_voices);
        ArrayPool<int>.Shared.Return(_starts);
        // cleared, so a pooled array never keeps resampled audio alive
        ArrayPool<AudioData<float>>.Shared.Return(_sounds, true);
        _voices = [];
        _starts = [];
        _sounds = [];
    }

    /// <summary>Adds an owner; it disposes the list once of its own.</summary>
    public void Retain()
    {
        Interlocked.Increment(ref _references);
    }

    /// <summary>First index whose start is at or after <paramref name="sample" />.</summary>
    internal int LowerBound(long sample)
    {
        var starts = Starts;
        int low = 0, high = starts.Length;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            if (starts[mid] < sample) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    /// <summary>
    ///     Adds voice <paramref name="index" />'s <c>[<paramref name="from" />, <paramref name="to" />)</c>
    ///     into <paramref name="left" /> and <paramref name="right" />, which start at output sample
    ///     <paramref name="blockStart" />. The range must lie inside the voice. Each sample gets the
    ///     voice's terms in a fixed order, so every way of slicing the song adds the same floats.
    /// </summary>
    internal void MixRange(int index, int from, int to, int blockStart, Span<float> left, Span<float> right)
    {
        ref readonly var voice = ref _voices[index];
        var samples = _sounds[voice.Sound].Samples;
        var source_left = samples[0];
        var source_right = samples.Length > 1 ? samples[1] : source_left;

        var plain_end = Math.Min(to, voice.CutAt);
        if (from < plain_end)
        {
            var source = voice.SourceOffset + (from - voice.Start);
            var length = plain_end - from;
            var destination = from - blockStart;

            if (voice.LeftFromLeft != 0)
                SampleMixer.Mix(source_left.AsSpan(source, length), left.Slice(destination, length),
                    voice.LeftFromLeft);
            if (voice.LeftFromRight != 0)
                SampleMixer.Mix(source_right.AsSpan(source, length), left.Slice(destination, length),
                    voice.LeftFromRight);
            if (voice.RightFromRight != 0)
                SampleMixer.Mix(source_right.AsSpan(source, length), right.Slice(destination, length),
                    voice.RightFromRight);
            if (voice.RightFromLeft != 0)
                SampleMixer.Mix(source_left.AsSpan(source, length), right.Slice(destination, length),
                    voice.RightFromLeft);
        }

        // the cut's fade, the same ramp SampleMixer.HandleCut put on the whole track
        for (var i = Math.Max(from, voice.CutAt); i < to; i++)
        {
            var fade = (float)(voice.CutAt + FadeLength - i) / FadeLength;
            var source = voice.SourceOffset + (i - voice.Start);
            var destination = i - blockStart;

            if (voice.LeftFromLeft != 0) left[destination] += source_left[source] * voice.LeftFromLeft * fade;
            if (voice.LeftFromRight != 0) left[destination] += source_right[source] * voice.LeftFromRight * fade;
            if (voice.RightFromRight != 0) right[destination] += source_right[source] * voice.RightFromRight * fade;
            if (voice.RightFromLeft != 0) right[destination] += source_left[source] * voice.RightFromLeft * fade;
        }
    }

    /// <summary>
    ///     Turns placements into voices in one pass. Owns the lookup tables it reuses from build
    ///     to build, so one builder serves one thread at a time.
    /// </summary>
    public sealed class Builder(SampleHolder holder, EncoderSettings settings)
    {
        private readonly Dictionary<(string, double), int> _soundIndex = new();
        private readonly Dictionary<(int End, int CutAt, int SourceOffset, int Sound), int> _stacked = new();
        private readonly Dictionary<string, int> _trackIndex = new();

        /// <summary>
        ///     Builds the voices of <paramref name="events" />, which must be sorted by index (as
        ///     <see cref="PlacementCalculator.CalculateMany" /> returns them), against the sounds in
        ///     <paramref name="processed" />.
        ///     <para>
        ///         Routing and cuts follow the legacy track renderer: a sound plays on its own track
        ///         when a sequence separates it, else on the default track; "!cut" cuts every track and
        ///         "#icut" the separated tracks it lists. A cut stops every voice on its tracks that was
        ///         placed before it and still sounds - placement order decides, so a sound at the cut's
        ///         own index placed after it plays on.
        ///     </para>
        /// </summary>
        public VoiceList Build(TimedEvents events, Dictionary<(string, double), ProcessedEvent> processed)
        {
            var placements = events.Placement;
            var sample_rate = settings.SampleRate;
            var fade = (int)(sample_rate / 1000) * (int)settings.CutFadeLengthMs;

            _trackIndex.Clear();
            foreach (var sequence in events.Sequences)
            foreach (var channel in sequence.SeparatedChannels)
                _trackIndex.TryAdd(Resolve(channel), _trackIndex.Count + 1); // 0 is the default track

            _soundIndex.Clear();
            var sounds = ArrayPool<AudioData<float>>.Shared.Rent(processed.Count);
            var longest_sound = 0;
            foreach (var (key, processed_event) in processed)
            {
                sounds[_soundIndex.Count] = processed_event.AudioData;
                longest_sound = Math.Max(longest_sound, processed_event.AudioData.Samples[0].Length);
                _soundIndex.Add(key, _soundIndex.Count);
            }

            // sized like the legacy renderer's buffers, so both render the same length
            var length = placements.Length > 0 ? (int)placements[^1].Index + longest_sound : 0;

            // Sized to the audible placements alone: a visualizer placement carries as many
            // visual ones again, and the voices are the biggest thing a long song keeps.
            var audible = 0;
            foreach (var placement in placements)
                if (placement.Audible)
                    audible++;

            var voices = ArrayPool<Voice>.Shared.Rent(audible);
            var next = ArrayPool<int>.Shared.Rent(audible);
            var track_count = _trackIndex.Count + 1;
            var rented_heads = track_count > 256 ? ArrayPool<int>.Shared.Rent(track_count) : null;
            var heads = rented_heads != null ? rented_heads.AsSpan(0, track_count) : stackalloc int[track_count];
            heads.Fill(-1);

            var count = 0;
            foreach (var placement in placements)
            {
                if (!placement.Audible) continue;

                var ev = placement.Event;
                var name = Resolve(ev.SoundEvent ?? string.Empty);
                var at = (int)placement.Index;

                if (ev is IndividualCutEvent individual_cut)
                {
                    foreach (var sound in individual_cut.CutSounds)
                        if (_trackIndex.TryGetValue(Resolve(sound), out var track))
                            Cut(track, at, voices, next, heads, fade);
                    continue;
                }

                if (name == "!cut")
                {
                    for (var track = 0; track < track_count; track++) Cut(track, at, voices, next, heads, fade);
                    continue;
                }

                if (ev is ICustomActionEvent || !_soundIndex.TryGetValue((name, ev.Value), out var sound_index))
                    continue;

                var audio = sounds[sound_index];
                var sound_length = audio.Samples[0].Length;
                var offset = 0;
                if (ev is ExtendedEvent { OffsetInSeconds: > 0 } extended)
                    offset = Math.Min((int)(extended.OffsetInSeconds * (sample_rate / Math.Pow(2, ev.Value / 12))),
                        sound_length);
                if (sound_length - offset <= 0) continue;

                ref var voice = ref voices[count];
                voice = new Voice
                {
                    Start = at,
                    End = (int)Math.Min((long)at + sound_length - offset, length),
                    CutAt = int.MaxValue,
                    SourceOffset = offset,
                    Sound = sound_index
                };
                SetGains(ref voice, ev, audio);

                var voice_track = _trackIndex.GetValueOrDefault(name);
                next[count] = heads[voice_track];
                heads[voice_track] = count;
                count++;
            }

            ArrayPool<int>.Shared.Return(next);
            if (rented_heads != null) ArrayPool<int>.Shared.Return(rented_heads);
            count = MergeStacked(voices, count);

            var starts = ArrayPool<int>.Shared.Rent(count);
            var longest = 0;
            for (var i = 0; i < count; i++)
            {
                starts[i] = voices[i].Start;
                longest = Math.Max(longest, voices[i].End - voices[i].Start);
            }

            return new VoiceList(voices, starts, sounds, count, length, longest, (int)settings.Channels, fade);
        }

        /// <summary>
        ///     Cuts every voice on <paramref name="track" /> placed since its last cut that still
        ///     sounds at <paramref name="at" />, and forgets them: a voice is cut once.
        /// </summary>
        private static void Cut(int track, int at, Voice[] voices, int[] next, Span<int> heads, int fade)
        {
            for (var i = heads[track]; i >= 0; i = next[i])
            {
                ref var voice = ref voices[i];
                if (voice.End <= at) continue;
                voice.CutAt = at;
                voice.End = (int)Math.Min(voice.End, (long)at + fade);
            }

            heads[track] = -1;
        }

        /// <summary>
        ///     Folds every voice that plays the same samples as an earlier one - same start, end,
        ///     cut, offset and sound - into that one, gains summed. Mixing is linear, so only the
        ///     rounding changes, and black MIDI stacks a third of its notes like this. Runs after
        ///     the cuts, which move ends. Keeps the order of what is left; returns its count.
        /// </summary>
        private int MergeStacked(Voice[] voices, int count)
        {
            var kept = 0;
            for (var run = 0; run < count;)
            {
                var end = run + 1;
                while (end < count && voices[end].Start == voices[run].Start) end++;

                if (end - run == 1)
                {
                    voices[kept++] = voices[run];
                    run = end;
                    continue;
                }

                _stacked.Clear();
                for (var i = run; i < end; i++)
                {
                    var voice = voices[i];
                    ref var into = ref CollectionsMarshal.GetValueRefOrAddDefault(_stacked,
                        (voice.End, voice.CutAt, voice.SourceOffset, voice.Sound), out var stacked);
                    if (!stacked)
                    {
                        into = kept;
                        voices[kept++] = voice;
                        continue;
                    }

                    ref var target = ref voices[into];
                    target.LeftFromLeft += voice.LeftFromLeft;
                    target.RightFromRight += voice.RightFromRight;
                    target.LeftFromRight += voice.LeftFromRight;
                    target.RightFromLeft += voice.RightFromLeft;
                }

                run = end;
            }

            return kept;
        }

        private string Resolve(string sound)
        {
            return holder.StringToSoundReferences.TryGetValue(sound, out var reference) ? reference.Id : sound;
        }

        /// <summary>
        ///     The gains the legacy renderer's per-channel pass ends up mixing with: pan first
        ///     (equal-power, where a stereo source cross-feeds the opposite channel into the side it
        ///     pans to, or the linear/log pan scales), then the volume scale on each term.
        /// </summary>
        private void SetGains(ref Voice voice, BaseEvent ev, AudioData<float> audio)
        {
            var volume = ev.WorkingVolume;
            var pan = ev is ExtendedEvent extended ? Math.Clamp(extended.Pan, -100f, 100f) : 0f;
            var stereo_output = settings.Channels >= 2;

            if (pan != 0 && settings.PanScale == PercentageScale.EqualPower)
            {
                var x = pan / 100f;
                if (audio.SourceChannels < 2 || !stereo_output)
                {
                    var angle = (x + 1f) * MathF.PI / 4f;
                    voice.LeftFromLeft = Gain(volume * MathF.Cos(angle));
                    if (stereo_output) voice.RightFromRight = Gain(volume * MathF.Sin(angle));
                    return;
                }

                var half_angle = (x <= 0 ? x + 1f : x) * MathF.PI / 2f;
                var cos = MathF.Cos(half_angle);
                var sin = MathF.Sin(half_angle);
                if (x <= 0)
                {
                    voice.LeftFromLeft = Gain(volume);
                    voice.LeftFromRight = Gain(volume * cos);
                    voice.RightFromRight = Gain(volume * sin);
                }
                else
                {
                    voice.LeftFromLeft = Gain(volume * cos);
                    voice.RightFromRight = Gain(volume);
                    voice.RightFromLeft = Gain(volume * sin);
                }

                return;
            }

            var left = volume;
            var right = volume;
            switch (pan)
            {
                case < 0:
                    right *= PanFactor(1f + pan / 100f);
                    break;
                case > 0:
                    left *= PanFactor(1f - pan / 100f);
                    break;
            }

            voice.LeftFromLeft = Gain(left);
            if (stereo_output) voice.RightFromRight = Gain(right);
        }

        private float PanFactor(float percent)
        {
            return settings.PanScale switch
            {
                PercentageScale.Logarithmic => MathF.Sqrt(percent),
                PercentageScale.LinearOverflowLogarithmic or PercentageScale.Linear => percent,
                _ => 0
            };
        }

        private float Gain(double volume)
        {
            return SampleMixer.FinalVolume(volume, settings.VolumeScale);
        }
    }
}
