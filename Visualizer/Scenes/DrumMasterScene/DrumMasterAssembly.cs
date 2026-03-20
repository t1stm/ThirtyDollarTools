using System.Reflection;

namespace DrumMasterScene;

public class DrumMasterAssembly
{
    public static Assembly Assembly { get; } = typeof(DrumMasterAssembly).Assembly;
}