namespace ThirtyDollarParser.Custom_Events;

public class IndividualCutEvent : BaseEvent, ICustomActionEvent, ICustomAudibleEvent
{
    public readonly HashSet<string> CutSounds;
    public bool IsStandardImplementation { get; set; }

    public IndividualCutEvent(HashSet<string> cutSounds, bool isStandardImplementation = true)
    {
        IsStandardImplementation = isStandardImplementation;
        SoundEvent ??= IsStandardImplementation ? "!cut" : "#icut";
        ValueScale = ValueScale.None;
        Value = 0;
        CutSounds = cutSounds;
    }

    public override string Stringify()
    {
        return string.Join('|', CutSounds.Select(sound => $"!cut@{sound}").ToArray());
    }

    public override IndividualCutEvent Copy()
    {
        return new IndividualCutEvent(CutSounds, IsStandardImplementation);
    }
}