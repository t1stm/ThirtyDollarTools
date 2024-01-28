namespace ThirtyDollarParser.Custom_Events;

public class IndividualCutEvent : Event
{
    public readonly string[] CutSounds;

    public IndividualCutEvent(string[] cut_sounds)
    {
        SoundEvent = "#icut";
        Value = 0;
        CutSounds = cut_sounds;
    }

    public override IndividualCutEvent Copy()
    {
        return new IndividualCutEvent(CutSounds);
    }
}