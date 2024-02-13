using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class ProcessedEvent
{
    public ProcessedEvent(Event ev)
    {
        _base_event = ev;
    }

    private readonly Event _base_event;
    public string? Name => _base_event.SoundEvent;
    public double Value => _base_event.Value;
    public AudioData<float> AudioData = AudioData<float>.Empty(2);

    public void ProcessAudioData(SampleProcessor processor)
    {
        AudioData = processor.ProcessEvent(_base_event);
    }
}