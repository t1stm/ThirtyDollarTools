using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog.Core;
using ThirtyDollarVisualizer.Engine.Assets;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Scenes.Arguments;

namespace ThirtyDollarVisualizer.Engine.Scenes;

public class SceneManager(Logger logger, AssetProvider assetProvider)
{
    public AssetProvider AssetProvider { get; } = assetProvider;
    public Dictionary<string, Scene> Scenes { get; } = new();
    public List<Scene> ActiveScenes { get; private set; } = [];
    
    public T LoadScene<T>(ReadOnlySpan<char> sceneName, Func<SceneManager, T> factory) where T : Scene
    {
        var scene = factory(this);
        lock (Scenes)
        {
            var alternativeLookup = Scenes.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alternativeLookup.TryAdd(sceneName, scene))
                throw new Exception($"Duplicated scene name: {sceneName}");
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
        
        foreach (var (sceneName, scene) in Scenes)
        {
            DebugMarker("Initializing scene: ", sceneName);
            scene.Initialize(initArguments);
        }
    }

    [Conditional("DEBUG")]
    private static void DebugMarker(ReadOnlySpan<char> message1, ReadOnlySpan<char> message2, bool hidden = false)
    {
        RenderMarker.Debug(message1, message2, hidden ? MarkerType.Hidden : MarkerType.Visible);
    }
    
    public void Resize(int eWidth, int eHeight)
    {
        foreach (var scene in Scenes.Values)
        {
            DebugMarker("Resizing scene: ", $"{scene.GetType().Name} {eWidth}x{eHeight}");
            scene.Resize(eWidth, eHeight);
        }
    }
    
    public void TransitionTo(ReadOnlySpan<Scene> scenes)
    {
        ActiveScenes.Clear();
        ActiveScenes.AddRange(scenes);
        foreach (var scene in ActiveScenes)
        {
            DebugMarker("Transitioning to scene: ", scene.GetType().Name);
            scene.TransitionedTo();
        }
    }
    
    public void TransitionTo(Scene scene)
    {
        TransitionTo([scene]);
    }

    public void TransitionTo(ReadOnlySpan<char> sceneName)
    {
        if (!Scenes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(sceneName, out var scene))
            throw new Exception($"Unable to find scene: {sceneName}");
        
        TransitionTo(scene);
    }

    public void Shutdown()
    {
        foreach (var scene in Scenes.Values) 
            scene.Shutdown();
        Scenes.Clear();
    }

    public void FileDropped(string[] locations)
    {
        foreach (var scene in ActiveScenes)
        {
            scene.FileDrop(locations);
        }
    }

    public void Keyboard(KeyboardState keyboardState)
    {
        foreach (var scene in ActiveScenes)
            scene.Keyboard(keyboardState);
    }

    public void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        foreach (var scene in ActiveScenes)
        {
            scene.Mouse(mouseState, keyboardState);
        }
    }

    public void Update(UpdateArguments updateArgs)
    {
        foreach (var scene in ActiveScenes)
        {
            DebugMarker("Updating scene: ", scene.GetType().Name, true);
            scene.Update(updateArgs);
        }
    }
}