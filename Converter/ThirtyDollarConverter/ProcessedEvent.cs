using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class ProcessedEvent(BaseEvent ev)
{
    public AudioData<float> AudioData = AudioData<float>.Empty(2);

    public string? Name => ev.SoundEvent;
    public double Value => ev.Value;

    public void ProcessAudioData(SampleProcessor processor)
    {
        AudioData = processor.ProcessEvent(ev);
    }
}