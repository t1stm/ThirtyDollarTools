namespace Shared;

/// <summary>
///     A scene the loading screen can hand off to. The loader fades one of these up
///     through itself as it takes itself off - see LoadingScene.Loader.ExitTo - so any
///     scene <c>--mode</c> can name has to be able to say how opaque it is.
/// </summary>
public interface IFadeInScene
{
    /// <summary>
    ///     Scene-wide opacity, driven from 0 to 1 by the loading screen. 1 on every later
    ///     entry, where it costs nothing.
    /// </summary>
    float InterfaceAlpha { get; set; }
}
