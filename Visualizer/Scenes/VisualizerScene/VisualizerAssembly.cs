using System.Reflection;

namespace VisualizerScene;

public static class VisualizerAssembly
{
    public static Assembly Assembly { get; } = typeof(VisualizerAssembly).Assembly;
}