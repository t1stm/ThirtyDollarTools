using System.Reflection;

namespace Components;

public static class ComponentsAssembly
{
    public static Assembly Assembly { get; } = typeof(ComponentsAssembly).Assembly;
}