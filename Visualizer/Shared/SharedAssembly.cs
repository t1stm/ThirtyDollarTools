using System.Reflection;

namespace Shared;

public static class SharedAssembly
{
    public static Assembly Assembly { get; } = typeof(SharedAssembly).Assembly;
}