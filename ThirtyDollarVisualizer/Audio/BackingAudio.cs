using ManagedBass;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class BackingAudio : AudibleBuffer
{
    private readonly int _sample_rate;
    private readonly int _channels;
    public BackingAudio(AudioData<float> data, int sample_rate) : base(data, sample_rate, 1)
    {
        _sample_rate = sample_rate;
        _channels = (int) data.ChannelCount;
    }

    public float GetCurrentTime()
    {
        var channels = Bass.SampleGetChannels(SampleHandle);
        var length = Bass.ChannelGetPosition(channels[0]);
        return length / (1f * _sample_rate * sizeof(float) * _channels);
    }

    public void Play(AudioContext context)
    {
        PlaySample(context, null, false);
    }

    public void SyncTime(TimeSpan player_time)
    {
        var time = GetCurrentTime();
        
        var delta = Math.Abs(time - (float)player_time.TotalSeconds);
        if (!(delta > 0.050f)) return; // 50 milliseconds 
        
        var channels = Bass.SampleGetChannels(SampleHandle);
        foreach (var channel in channels)
        {
            var position = (long)(player_time.TotalSeconds * (_sample_rate * _channels) * sizeof(float));
            Console.WriteLine($"{DateTime.Now} OOS / Delta: {delta} / Time: {time} / Position: {position}");
            Bass.ChannelSetPosition(channel, position);
        }
    }

    public void UpdatePlayState(bool playing)
    {
        switch (playing)
        {
            case true:
            {
                var channels = Bass.SampleGetChannels(SampleHandle);
                foreach (var channel in channels)
                {
                    Bass.ChannelPlay(channel);
                }

                break;
            }
            
            case false:
            {
                var channels = Bass.SampleGetChannels(SampleHandle);
                foreach (var channel in channels)
                {
                    Bass.ChannelPause(channel);
                }

                break;
            }
        }
    }
}