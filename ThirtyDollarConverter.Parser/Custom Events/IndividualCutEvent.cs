namespace ThirtyDollarParser.Custom_Events;

public class IndividualCutEvent : BaseEvent, ICustomActionEvent, ICustomAudibleEvent
{
    public readonly HashSet<string> CutSounds;
    public bool IsStandardImplementation { get; set; }

    public IndividualCutEvent(HashSet<string> cut_sounds)
    {
        SoundEvent ??= IsStandardImplementation ? "!cut" : "#icut";
        ValueScale = ValueScale.None;
        Value = 0;
        CutSounds = cut_sounds;
    }

    public override IndividualCutEvent Copy()
    {
        return new IndividualCutEvent(CutSounds)
        {
            IsStandardImplementation = IsStandardImplementation
        };
    }
}