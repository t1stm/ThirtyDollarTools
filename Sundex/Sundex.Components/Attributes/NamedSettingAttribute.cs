using JetBrains.Annotations;

namespace Sundex.Components.Attributes;

/// <summary>Names a property the stylesheet sets by reflection, so its setter has no caller in code.</summary>
[MeansImplicitUse(ImplicitUseKindFlags.Assign)]
[AttributeUsage(AttributeTargets.Property)]
public class NamedSettingAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}