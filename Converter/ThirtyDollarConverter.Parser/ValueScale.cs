namespace ThirtyDollarConverter.Parser;

/// <summary>
///     How an event's value combines with the running one. <see cref="None" /> is first so it
///     is the default member: an event built in code without stating a scale means "no scale"
///     rather than picking up one of the arithmetic modes.
/// </summary>
public enum ValueScale
{
    None,
    Divide,
    Times,
    Add
}