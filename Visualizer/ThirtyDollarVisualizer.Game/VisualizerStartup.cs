using System.Reflection;
using DrumMasterScene;
using EditorScene;
using HomeScene;
using LoadingScene;
using SettingsScene;
using Shared;
using Shared.Updates;
using Sundex.Components;
using Sundex.Engine;
using Sundex.Engine.Audio;
using ThirtyDollarConverter;
using VisualizerScene;
using VisualizerScene.Settings;

namespace ThirtyDollarVisualizer;

/// <summary>What the desktop and browser builds share from boot: the asset assemblies and the scenes.</summary>
public static class VisualizerStartup
{
    public static Assembly[] GetAssetAssemblies(Assembly hostAssembly) =>
    [
        hostAssembly,
        SharedAssembly.Assembly,
        ComponentsAssembly.Assembly,
        LoaderAssembly.Assembly,
        HomeAssembly.Assembly,
        VisualizerAssembly.Assembly,
        EditorAssembly.Assembly,
        DrumMasterAssembly.Assembly,
        SettingsAssembly.Assembly
    ];

    /// <summary>Queues the loading screen, which builds every scene and then fades into <paramref name="mode" />.</summary>
    /// <param name="mode">
    ///     The scene to open once loaded, by the name LoadScene registers: "visualizer",
    ///     "drum-master", "editor", "settings", or one an <paramref name="additionalPreloads" />
    ///     entry adds. Null, or an unknown one, opens the home screen - see Loader.UpdateExit.
    /// </param>
    /// <param name="samples">Where sounds download from and to; null for the site and the local cache.</param>
    public static void Start(GameRuntime runtime, AudioDevice? audioDevice, VisualizerSettings settings,
        Workspace workspace, string? mode = null, string? sequence = null, string? greeting = null,
        float? scale = null, ScenePreload[]? additionalPreloads = null, SampleHolder? samples = null)
    {
        runtime.Globals.Set("visualizer-settings", settings);
        runtime.Enqueue(instance =>
        {
            var version = VersionInfo.Read(instance.AssetProvider);
            instance.SceneManager.LoadScene<Loader>("loader",
                _ => new Loader(instance, audioDevice, settings, version, workspace, samples)
                {
                    Preloads = BuildPreloads(settings, version, mode, sequence, greeting, scale,
                        additionalPreloads ?? []),
                    ExitTo = mode ?? "home"
                });
        });
        runtime.Enqueue(instance => instance.SceneManager.TransitionTo("loader"));
    }

    /// <summary>
    ///     Every scene the program has, and what the loading screen says while each is built.
    ///     They are built before the sound download starts, so the work lands while the loading
    ///     screen is animating rather than between the download finishing and the home screen.
    /// </summary>
    private static ScenePreload[] BuildPreloads(VisualizerSettings settings, VersionInfo? version, string? mode,
        string? sequence, string? greeting, float? scale, ScenePreload[] additionalPreloads) =>
    [
        // Home first - the loading screen's exit fades into it, so it has to exist before the
        // rest are worth building.
        new("Preparing the home screen", (runtime, _) =>
            runtime.SceneManager.LoadScene<Home>("home",
                _ => new Home(runtime, version?.Display ?? VersionInfo.DeveloperBuild, () => settings.CheckForUpdates))),

        new("Preparing the visualizer", (runtime, workflow) =>
            runtime.SceneManager.LoadScene<Visualizer>("visualizer",
                _ => new Visualizer(runtime, settings, workflow, [sequence])
                {
                    Greeting = greeting,
                    Scale = scale ?? 1f
                })),

        new("Preparing Drum Master", (runtime, workflow) =>
            runtime.SceneManager.LoadScene<DrumMaster>("drum-master", _ => new DrumMaster(runtime, workflow))),

        // -i is the visualizer's sequence unless the editor is the one being opened, in
        // which case that is what the editor opens.
        new("Preparing the editor", (runtime, workflow) =>
            runtime.SceneManager.LoadScene<Editor>("editor",
                _ => new Editor(runtime, workflow, mode == "editor" ? sequence : null))),

        new("Preparing settings", (runtime, _) =>
            runtime.SceneManager.LoadScene<Settings>("settings", _ => new Settings(runtime, settings))),

        .. additionalPreloads
    ];
}
