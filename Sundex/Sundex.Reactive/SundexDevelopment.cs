namespace Sundex.Reactive;

/// <summary>Enables Sundex diagnostics and development-time behavior for the running application.</summary>
public static class SundexDevelopment
{
    public static bool Enabled { get; } = AppContext.TryGetSwitch("Sundex.Development", out var enabled) && enabled;
}
