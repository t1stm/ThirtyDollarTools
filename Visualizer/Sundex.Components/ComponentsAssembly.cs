using System.Reflection;

namespace Sundex.Components;

public static class ComponentsAssembly
{
    public static Assembly Assembly { get; } = typeof(ComponentsAssembly).Assembly;
}