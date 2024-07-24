namespace ThirtyDollarParser.Custom_Events;

public class IndividualCutEvent : BaseEvent, ICustomActionEvent, ICustomAudibleEvent
{
    public readonly HashSet<string> CutSounds;

    public IndividualCutEvent(HashSet<string> cut_sounds)
    {
        SoundEvent ??= "#icut";
        ValueScale = ValueScale.None;
        Value = 0;
        CutSounds = cut_sounds;
    }

    public override IndividualCutEvent Copy()
    {
        return new IndividualCutEvent(CutSounds);
    }
}