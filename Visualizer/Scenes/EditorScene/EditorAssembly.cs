using System.Reflection;

namespace EditorScene;

public static class EditorAssembly
{
    public static Assembly Assembly { get; } = typeof(EditorAssembly).Assembly;
}