using System.Runtime.Versioning;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Serilog.Events;
using Sundex.Engine;
using Sundex.Engine.Audio;
using Sundex.Engine.Input;
using Sundex.Engine.Renderer.OpenGL;
using ThirtyDollarConverter;
using VisualizerScene;
using VisualizerScene.Settings;

namespace ThirtyDollarVisualizer.WASM;

/// <summary>The page's canvas as the runtime's window: it holds the input between frames and runs each one.</summary>
[SupportedOSPlatform("browser")]
internal sealed class BrowserHost : IGameHost
{
    private readonly GameRuntime _runtime;
    private readonly GLGraphicsDevice _device;
    private readonly AudioDevice _audio;
    private readonly bool _verify;
    private KeySet _heldKeys;
    private KeySet _pressedKeys;
    private Vector2 _lastPointer;

    public BrowserHost(Vector2i clientSize, Vector2i framebufferSize, Uri assetBase, BrowserWorkspace workspace,
        bool verify)
    {
        ClientSize = clientSize;
        FramebufferSize = framebufferSize;
        _verify = verify;

        BrowserContext.Open();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(verify ? LogEventLevel.Debug : LogEventLevel.Information)
            .WriteTo.Console().CreateLogger();
        _device = GLGraphicsDevice.CreateWebGL2(logger);
        _audio = AudioDevice.Open(logger);
        _runtime = new GameRuntime(logger, VisualizerStartup.GetAssetAssemblies(typeof(BrowserHost).Assembly),
            _device, this);
        _runtime.Initialize();

        var samples = new SampleHolder(logger)
        {
            AssetBaseUri = assetBase,
            SamplesLocation = "/visualizer-cache/sounds"
        };
        VisualizerStartup.Start(_runtime, _audio, LoadSettings(), workspace, samples: samples);
    }

    public Vector2i ClientSize { get; private set; }
    public Vector2i FramebufferSize { get; private set; }
    public bool IsCursorInWindow { get; private set; }

    /// <summary>Frames come from requestAnimationFrame, which runs at the display's rate.</summary>
    public VSyncMode VSync => VSyncMode.On;

    public string Title
    {
        get;
        set
        {
            field = value;
            Program.SetTitle(value);
        }
    } = "Thirty Dollar Visualizer";

    public MouseCursor Cursor
    {
        get;
        set
        {
            field = value;
            Program.SetCursor(CursorCss(value.Shape));
        }
    } = MouseCursor.Default;

    /// <summary>A page can't read the clipboard without asking, so this gives back what was last copied.</summary>
    public string ClipboardString
    {
        get;
        set
        {
            field = value;
            Program.WriteClipboard(value);
        }
    } = "";

    /// <summary>A page can't close its own tab.</summary>
    public void Close() { }

    public string ActiveScene => _runtime.SceneManager.ActiveScenes.FirstOrDefault()?.GetType().Name ?? "";
    public double PlaybackMilliseconds => _runtime.SceneManager.Get<Visualizer>().PlaybackMilliseconds;

    public void Resize(Vector2i clientSize, Vector2i framebufferSize)
    {
        ClientSize = clientSize;
        FramebufferSize = framebufferSize;
        _runtime.Resize(framebufferSize);
    }

    public void Tick(double delta, Vector2 position, Vector2 scroll, uint buttons, uint previousButtons,
        uint pressed, uint released, bool inside)
    {
        IsCursorInWindow = inside;
        var pointer = new PointerInput(position, position - _lastPointer, scroll, buttons, previousButtons, pressed,
            released);
        _lastPointer = position;
        _runtime.Update(delta, pointer, new KeyboardInput(_heldKeys, _pressedKeys));
        _pressedKeys.Clear();
        _runtime.Render(delta);
        if (_verify) _device.ThrowIfError();
    }

    public bool KeyDown(string code, string text, bool repeat, bool shift, bool control, bool alt, bool meta,
        bool capsLock, bool numLock)
    {
        var key = DomKeys.FromCode(code);
        if (key != Keys.Unknown)
        {
            if (!_heldKeys.Contains(key)) _pressedKeys.Add(key);
            _heldKeys.Add(key);
            var modifiers = DomKeys.Modifiers(shift, control, alt, meta, capsLock, numLock);
            _runtime.SceneManager.KeyDown(new KeyboardKeyEventArgs(key, 0, modifiers, repeat));
        }

        foreach (var rune in text.EnumerateRunes()) _runtime.SceneManager.TextInput(new TextInputEventArgs(rune.Value));
        return key != Keys.Unknown || text.Length > 0;
    }

    public void KeyUp(string code) => _heldKeys.Remove(DomKeys.FromCode(code));

    /// <summary>Lets go of every key, for when the canvas loses focus and their key-ups go elsewhere.</summary>
    public void ClearKeys()
    {
        _heldKeys.Clear();
        _pressedKeys.Clear();
    }

    public void PumpAudio() => _audio.Pump();

    /// <summary>The settings from the page's storage, written back on every change.</summary>
    private static VisualizerSettings LoadSettings()
    {
        var settings = new VisualizerSettings();
        if (Program.ReadSettings() is { } text) SettingsText.Read(settings, text);
        // There is nothing to update in the browser, so there is no setup question about it either.
        settings.UpdateCheckAsked = true;
        settings.CheckForUpdates = false;
        Keybinds.Attach(settings);
        settings.Changed += _ => Program.WriteSettings(SettingsText.Write(settings));
        return settings;
    }

    private static string CursorCss(MouseCursor.StandardShape shape) => shape switch
    {
        MouseCursor.StandardShape.IBeam => "text",
        MouseCursor.StandardShape.Crosshair => "crosshair",
        MouseCursor.StandardShape.PointingHand => "pointer",
        MouseCursor.StandardShape.ResizeEW => "ew-resize",
        MouseCursor.StandardShape.ResizeNS => "ns-resize",
        MouseCursor.StandardShape.ResizeNESW => "nesw-resize",
        MouseCursor.StandardShape.ResizeNWSE => "nwse-resize",
        MouseCursor.StandardShape.ResizeAll => "move",
        MouseCursor.StandardShape.NotAllowed => "not-allowed",
        _ => "default"
    };
}
