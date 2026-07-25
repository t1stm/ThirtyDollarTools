using System.Reflection;

namespace LoadingScene;

public static class LoaderAssembly
{
    public static Assembly Assembly { get; } = typeof(LoaderAssembly).Assembly;
}