using OpenTK.Mathematics;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Inputs;

/// <summary>
///     The input components' default shades, from the editor's instrument builder (the field look
///     every scene shares): a sheet only has to restate them to differ.
/// </summary>
internal static class FieldColors
{
    /// <summary>
    ///     A text field's box, and a checkbox's: darker than every card it sits on, so it shows on
    ///     all of them (the step shade below is a card's own, the inspector's keyframe card).
    /// </summary>
    public static readonly Vector4 Fill = new ColorValue("#262936").Vector;

    /// <summary>The small square buttons on a field: a number's - and +.</summary>
    public static readonly Vector4 Step = new ColorValue("#353a54").Vector;

    /// <summary>A placeholder's text.</summary>
    public static readonly Vector4 Placeholder = new ColorValue("#565f89").Vector;

    /// <summary>A ticked checkbox's mark.</summary>
    public static readonly Vector4 Check = new ColorValue("#7aa2f7").Vector;
}
