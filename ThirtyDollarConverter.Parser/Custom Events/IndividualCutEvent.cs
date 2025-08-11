namespace ThirtyDollarParser.Custom_Events;

public class IndividualCutEvent : BaseEvent, ICustomActionEvent, ICustomAudibleEvent
{
    public readonly HashSet<string> CutSounds;

    public IndividualCutEvent(HashSet<string> cutSounds)
    {
        SoundEvent ??= "#icut";
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
        return new IndividualCutEvent(CutSounds);
    }
}