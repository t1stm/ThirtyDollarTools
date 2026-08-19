using Shared;
using Sundex.Engine;

namespace LoadingScene;

/// <summary>
///     One scene for the loading screen to build before it lets the sounds start coming
///     down. The program that owns the scenes declares these; <see cref="Loader" /> owns
///     when they run and what is on screen while they do.
/// </summary>
/// <param name="Message">What the status line says while this one is being built.</param>
/// <param name="Load">
///     Builds and registers the scene. Runs on the render thread, one per frame, and is
///     handed the workflow every tool scene shares.
/// </param>
public record ScenePreload(string Message, Action<Game, ThirtyDollarWorkflow> Load);
