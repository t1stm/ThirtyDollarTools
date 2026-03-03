namespace Sundex.Components.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class NamedSettingAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}