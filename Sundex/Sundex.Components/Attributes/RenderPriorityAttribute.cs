namespace Sundex.Components.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class RenderPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}