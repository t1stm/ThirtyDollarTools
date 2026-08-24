using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Sundex.Engine.Scenes.Arguments;

namespace Sundex.Engine.Scenes;

public class SceneManager(ILogger logger)
{
    private List<Scene>? _nextScenes = [];
    public Dictionary<string, Scene> Scenes { get; } = new();
    public List<Scene> ActiveScenes { get; private set; } = [];

    private Queue<Scene> ScenesToInitialize { get; } = [];

    public T LoadScene<T>(ReadOnlySpan<char> sceneName, Func<SceneManager, T> factory) where T : Scene
    {
        // Timed because building a scene happens on the render thread and is the one thing
        // in this program that reliably costs whole frames. The number is what tells you
        // whether a hitch is worth chasing, and which scene to chase it in.
        var stopwatch = Stopwatch.StartNew();
        var scene = factory(this);
        logger.Debug("[Scene Manager] Built {Scene} in {Elapsed} ms",
            scene.GetType().Name, stopwatch.ElapsedMilliseconds);
        lock (Scenes)
        {
            var alternativeLookup = Scenes.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alternativeLookup.TryAdd(sceneName, scene))
                throw new Exception($"Duplicated scene name: {sceneName}");
        }

        lock (ScenesToInitialize)
        {
            ScenesToInitialize.Enqueue(scene);
        }

        return scene;
    }

    public void Render(RenderArguments renderArgs)
    {
        foreach (var scene in CollectionsMarshal.AsSpan(ActiveScenes))
        {
            DebugMarker("Rendering scene: ", scene.GetType().Name, true);
            scene.Render(renderArgs);
        }
    }

    public void Initialize(InitArguments initArguments)
    {
        while (ScenesToInitialize.TryDequeue(out var scene))
        {
            DebugMarker("Initializing scene: ", scene.GetType().Name);
            var stopwatch = Stopwatch.StartNew();
            scene.Initialize(initArguments);
            logger.Debug("[Scene Manager] Initialized {Scene} in {Elapsed} ms",
                scene.GetType().Name, stopwatch.ElapsedMilliseconds);
        }
    }

    [Conditional("DEBUG")]
    private static void DebugMarker(ReadOnlySpan<char> message1, ReadOnlySpan<char> message2, bool hidden = false)
    {
        RenderMarker.Debug(message1, message2, hidden ? MarkerType.Hidden : MarkerType.Visible);
    }

    public void Resize(int eWidth, int eHeight)
    {
        lock (Scenes)
        {
            foreach (var (_, scene) in Scenes)
            {
                DebugMarker("Resizing scene: ", $"{scene.GetType().Name} {eWidth}x{eHeight}");
                scene.Resize(eWidth, eHeight);
            }
        }
    }

    public void TransitionTo(ReadOnlySpan<Scene> scenes)
    {
        lock (ActiveScenes)
        {
            _nextScenes = [];
            _nextScenes.AddRange(scenes);

            foreach (var scene in _nextScenes)
            {
                DebugMarker("Transitioning to scene: ", scene.GetType().Name);
                scene.TransitionedTo();
            }
        }
    }

    public void TransitionTo(Scene scene)
    {
        TransitionTo([scene]);
    }

    public void TransitionTo(ReadOnlySpan<char> sceneName)
    {
        lock (Scenes)
        {
            if (!Scenes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(sceneName, out var scene))
                throw new Exception($"Unable to find scene: {sceneName}");

            TransitionTo(scene);
        }
    }

    /// <summary>
    ///     Applies a hot reload to every loaded scene, not just the active ones: a scene
    ///     reloaded only when it is next shown would come back stale, and the scenes that
    ///     are not on screen are the cheap ones anyway.
    ///     <para>
    ///         Each scene is isolated. A markup file saved halfway through an edit throws
    ///         while it is being parsed, and the scene that failed keeps the UI it already
    ///         had - the error goes to the log and the other scenes still reload, so a typo
    ///         costs a log line rather than the running program.
    ///     </para>
    /// </summary>
    public void Reload(ReloadScope scope)
    {
        lock (Scenes)
        {
            var stopwatch = Stopwatch.StartNew();
            var reloaded = 0;

            foreach (var scene in Scenes.Values)
                try
                {
                    if (scope == ReloadScope.Styles) scene.ReloadStyles();
                    else scene.ReloadUI();
                    reloaded++;
                }
                catch (Exception exception)
                {
                    logger.Error(exception, "[Hot Reload] {Scene} failed to reload; keeping the UI it had",
                        scene.GetType().Name);
                }

            logger.Information("[Hot Reload] {Scope} reload of {Count} scene(s) in {Elapsed} ms",
                scope, reloaded, stopwatch.ElapsedMilliseconds);
        }
    }

    public void Shutdown()
    {
        lock (Scenes)
        {
            foreach (var scene in Scenes.Values)
                scene.Shutdown();
            Scenes.Clear();
        }
    }

    public void FileDropped(string[] locations)
    {
        lock (ActiveScenes)
        {
            foreach (var scene in ActiveScenes) scene.FileDrop(locations);
        }
    }

    public void Keyboard(KeyboardState keyboardState)
    {
        lock (ActiveScenes)
        {
            foreach (var scene in ActiveScenes)
                scene.Keyboard(keyboardState);
        }
    }

    public void TextInput(TextInputEventArgs e)
    {
        lock (ActiveScenes)
        {
            foreach (var scene in ActiveScenes) scene.TextInput(e);
        }
    }

    public void KeyDown(KeyboardKeyEventArgs e)
    {
        lock (ActiveScenes)
        {
            foreach (var scene in ActiveScenes) scene.KeyDown(e);
        }
    }

    public void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        lock (ActiveScenes)
        {
            foreach (var scene in ActiveScenes) scene.Mouse(mouseState, keyboardState);
        }
    }

    public void Update(UpdateArguments updateArgs)
    {
        lock (ActiveScenes)
        {
            if (_nextScenes != null)
                ActiveScenes = _nextScenes;

            foreach (var scene in ActiveScenes)
            {
                DebugMarker("Updating scene: ", scene.GetType().Name, true);
                scene.Update(updateArgs);
            }
        }
    }

    public T Get<T>() where T : Scene
    {
        foreach (var (_, scene) in Scenes)
            if (scene is T scene1)
                return scene1;

        throw new Exception("Unable to find scene");
    }
}