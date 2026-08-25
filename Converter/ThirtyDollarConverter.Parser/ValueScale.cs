namespace ThirtyDollarConverter.Parser;

/// <summary>
///     How an event's value combines with the running one. <see cref="None" /> is first so it
///     is the default member: an event built in code without stating a scale means "no scale",
///     and anything else silently turned every such event into a divide (which is how exported
///     gaps came out as "_pause@/").
/// </summary>
public enum ValueScale
{
    None,
    Divide,
    Times,
    Add
}