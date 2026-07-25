using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EditorScene.Tests")]

namespace EditorScene;

public static class EditorAssembly
{
    public static Assembly Assembly { get; } = typeof(EditorAssembly).Assembly;
}