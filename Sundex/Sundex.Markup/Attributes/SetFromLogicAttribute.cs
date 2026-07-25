using JetBrains.Annotations;

namespace Sundex.Markup.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
[MeansImplicitUse]
public class SetFromLogicAttribute : Attribute;