using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class Greeting(AudioContext context, BufferHolder holder)
{
    private GreetingType _greeting;
    public GreetingType GreetingType
    {
        get => _greeting;
        set => SetBuffer(value);
    }
    public AudibleBuffer? AudibleBuffer { get; set; }
    public int LengthMiliseconds;
    public void SetBuffer(GreetingType greeting)
    {
        _greeting = greeting;
        if (greeting == GreetingType.None)
        {
            AudibleBuffer = null;
            LengthMiliseconds = 0;
            return;
        }

        var greeting_name = $"greeting_{(int)greeting}";
        holder.TryGetBuffer(greeting_name, 0, out var buffer);
        
        var audio_data = AudioData<float>.Empty(2);
        var sample_rate = 48000;
        
        AudibleBuffer = context.GetBufferObject(audio_data, sample_rate);
        LengthMiliseconds = audio_data.GetLength() * 1000 / sample_rate;
    }

    public async Task PlayWaitFinish()
    {
        AudibleBuffer?.Play();
        await Task.Delay(LengthMiliseconds);
    }
}

public enum GreetingType : int
{
    None,
    DontLectureMeWithYour30DollarHaircut = 1,
    HowYouGonnaTalkBehindMyBackWhenYouDeadassBuiltLikeA = 2,
    WhitePeopleBeLike = 3,
    
    DontLectureMeWithYour30DollarVisualizer = 4
}