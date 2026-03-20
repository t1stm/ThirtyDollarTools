using System.Reflection;

namespace SettingsScene;

public class SettingsAssembly
{
    public static Assembly Assembly { get; } = typeof(SettingsAssembly).Assembly;
}