namespace Sundex.Components.Attributes;

/// <summary>
///     The tag markup builds this component by: <c>[MarkupTag("text-input")]</c> makes
///     <c>&lt;text-input/&gt;</c>. The component needs a public constructor taking just a
///     <see cref="Abstractions.UIContext" />; attributes then set its public properties, by their
///     <see cref="NamedSettingAttribute" /> name or by their C# name in kebab case.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MarkupTagAttribute(string tag) : Attribute
{
    public string Tag { get; } = tag;
}
