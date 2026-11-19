namespace Sundex.Components.Abstractions;

public enum UIState
{
    None,
    Hovered,
    Pressed,
    Disabled,
    Errored,

    /// <summary>Holds keyboard focus, and the pointer isn't on it.</summary>
    Focused
}