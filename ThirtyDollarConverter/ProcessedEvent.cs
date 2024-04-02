using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class ProcessedEvent
{
    private readonly BaseEvent _base_event;
    public AudioData<float> AudioData = AudioData<float>.Empty(2);

    public ProcessedEvent(BaseEvent ev)
    {
        _base_event = ev;
    }

    public string? Name => _base_event.SoundEvent;
    public double Value => _base_event.Value;

    public void ProcessAudioData(SampleProcessor processor)
    {
        AudioData = processor.ProcessEvent(_base_event);
    }
}