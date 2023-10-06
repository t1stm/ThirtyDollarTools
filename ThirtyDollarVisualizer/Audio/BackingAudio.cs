using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class BackingAudio
{
    private readonly int _sample_rate;
    private readonly int _channels;
    private AudioContext _context;
    private AudibleBuffer _buffer;
    public BackingAudio(AudioContext context, AudioData<float> data, int sample_rate)
    {
        _sample_rate = sample_rate;
        _channels = (int) data.ChannelCount;
        _context = context;
        _buffer = context.GetBufferObject(data, sample_rate);
    }

    public long GetCurrentTime()
    {
        return _buffer.GetTime_Milliseconds();
    }

    public void Play()
    {
        _buffer.Play();
    }

    public void SyncTime(TimeSpan player_time)
    {
        var time = GetCurrentTime() / 1000f;
        
        var delta = Math.Abs(time - (float)player_time.TotalSeconds);
        if (!(delta > 0.050f)) return; // 50 milliseconds 
        
        var position = (long)(player_time.TotalSeconds * 1000);
        Console.WriteLine($"{DateTime.Now} OOS / Delta: {delta} / Time: {time} / Position: {position}");
        _buffer.SeekTime_Milliseconds(position);
    }

    public void UpdatePlayState(bool playing)
    {
        _buffer.SetPause(!playing);
    }
}