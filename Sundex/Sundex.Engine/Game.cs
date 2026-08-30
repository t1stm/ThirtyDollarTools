using System.ComponentModel;
using System.Reflection;
using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Engine.Threading;

namespace Sundex.Engine;

public class Game : GameWindow
{
    private readonly Queue<Action<Game>> _enqueuedEvents = new();
    private readonly string _id;
    private readonly ILogger _loggerGL;

    private GLDebugProc _storedDebugCallback = null!; // exists due to .NET design
    private WindowState _preFullscreenState = WindowState.Normal;

#if DEBUG
    private readonly SourceWatcher _sourceWatcher;
#endif

    public Game(ILogger serilogLogger, Assembly[] assemblies, GameWindowSettings gameSettings,
        NativeWindowSettings nativeWindowSettings, string id) :
        base(gameSettings, nativeWindowSettings)
    {
        _id = id;

        Logger = serilogLogger.ForContext<Game>();
        _loggerGL = Logger.ForContext("SourceContext", "OpenGL");

        var callingAssembly = Assembly.GetExecutingAssembly();
        AssetAssemblies = [callingAssembly, .. assemblies];

        AssetProvider = new AssetProvider(Logger, AssetAssemblies, GLInfo);
        SceneManager = new SceneManager(Logger);

#if DEBUG
        // Reloads land on the frame queue rather than running where they were raised: both
        // the IDE's hot-reload callback and the file watcher fire on their own threads, and
        // rebuilding a UI touches GL objects that belong to the render thread.
        HotReload.Requested = scope => Enqueue(game => game.SceneManager.Reload(scope));
        _sourceWatcher = new SourceWatcher(Logger, AssetProvider.SourceRoots);
#endif
    }

    public ILogger Logger { get; }
    public Assembly[] AssetAssemblies { get; }
    public AssetProvider AssetProvider { get; }
    public SceneManager SceneManager { get; }
    public ThreadRunner ThreadRunner => AssetProvider.ThreadRunner;

    public GameGlobals Globals { get; } = new();

    /// <summary>
    ///     Text shown when the fullscreen shortcut is pressed on Wayland - see
    ///     <see cref="OnWindowActionUnavailable" />.
    /// </summary>
    public const string WaylandFullscreenMessage =
        "Fullscreen can't be toggled from inside the app on Wayland:\n" +
        "GLFW leaves window state to the compositor.\n\n" +
        "Use your window manager's own fullscreen shortcut instead\n" +
        "(often Super+F or F11).";

    /// <summary>
    ///     Raised when a window action the platform refuses is attempted; today only
    ///     fullscreen on Wayland. The engine has no UI of its own, so the active scene
    ///     wires this to whatever it shows dialogs with - unwired, the message only
    ///     reaches the log.
    /// </summary>
    public Action<string>? OnWindowActionUnavailable { get; set; }
    private GLInfo GLInfo { get; } = new();

    /// <summary>
    ///     Ratio between physical framebuffer pixels and logical window (client) size, measured
    ///     as FramebufferSize / ClientSize rather than read from the platform's reported DPI or
    ///     content scale, which can disagree with the real framebuffer (on X11 a desktop scale
    ///     may only change Xft.dpi while the framebuffer stays 1:1 with the window). Correct on
    ///     every platform by construction, so no per-platform special-casing. False when the
    ///     client size is not yet known, in which case both scales come back as 1.
    /// </summary>
    public bool TryGetScreenScale(out float horizontalScale, out float verticalScale)
    {
        var client = ClientSize;
        var framebuffer = FramebufferSize;

        if (client.X <= 0 || client.Y <= 0)
        {
            horizontalScale = 1f;
            verticalScale = 1f;
            return false;
        }

        horizontalScale = (float)framebuffer.X / client.X;
        verticalScale = (float)framebuffer.Y / client.Y;
        return true;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GetGLInfo(GLInfo);
        _loggerGL.ForContext<GLInfo>().Information("{@GLInfo}", GLInfo);

        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.Multisample);

        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        // .NET GC automatically collects this unless it's stored somewhere in a class.
        // See: https://opentk.net/learn/appendix_opengl/debug_callback.html
        _storedDebugCallback = DebugCallback;

        if (GLInfo.SupportsKHRDebug)
            GL.DebugMessageCallback(_storedDebugCallback, in IntPtr.Zero);
        else RenderMarker.Enabled = false;

        GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
        GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

        RenderMarker.Debug("Game Window Initialized");
        foreach (var assembly in AssetAssemblies) ReflectionPreloadObjects(assembly);

        AppDomain.CurrentDomain.UnhandledException +=
            (_, e) =>
            {
                Logger.Fatal(e.ExceptionObject as Exception,
                    "[Unhandled Exception]: ({GameName}, {Id}) ", nameof(Game), _id);
            };

        // Some windowing backends don't reliably deliver a framebuffer-resize event on the
        // very first frame, so the viewport and the scenes are sized from the current
        // framebuffer here as well as from the event.
        var framebufferSize = FramebufferSize;
        ApplyFramebufferSize(framebufferSize.X, framebufferSize.Y);

        RenderMarker.Debug("Finished OnLoad() Procedure");
    }

    private void ReflectionPreloadObjects(Assembly targetAssembly)
    {
        const string preloadMethodName = "Preload";

        // who doesn't love reflection in a small game engine?
        var types = targetAssembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<PreloadGraphicsContextAttribute>() != null);

        foreach (var type in types)
        {
            if (!typeof(IGamePreloadable).IsAssignableFrom(type))
                continue;

            var method = type.GetMethod(preloadMethodName);
            if (method is null)
                throw new Exception("Method not found");

            method.Invoke(null, [AssetProvider]);
        }
    }

    private unsafe void DebugCallback(DebugSource source, DebugType type, uint id,
        DebugSeverity severity, int length, IntPtr messagePtr, IntPtr userParameter)
    {
        switch (type)
        {
            case DebugType.DebugTypeOther:
            case DebugType.DebugTypeMarker when id == 1:
                return;
        }

        var stringFromPointer = new ReadOnlySpan<byte>(messagePtr.ToPointer(), length);
        Span<char> stringBuffer = stackalloc char[stringFromPointer.Length];
        Encoding.UTF8.GetChars(stringFromPointer, stringBuffer);

        var sourceText = source != DebugSource.DontCare ? source.ToString()[11..] : "Unknown";
        var typeText = type != DebugType.DontCare ? type.ToString()[9..] : "Unknown";
        var severityText = severity != DebugSeverity.DontCare ? severity.ToString()[13..] : "Unknown";

        _loggerGL.Debug("{SourceText}, ({TypeText}, {Id}) {SeverityText}: {CallbackMessage}",
            sourceText, typeText, id, severityText, stringBuffer.ToString());
    }

    private static void GetGLInfo(GLInfo glInfo)
    {
        var extCount = GL.GetInteger(GetPName.NumExtensions);
        glInfo.Extensions.EnsureCapacity(extCount);

        for (uint i = 0; i < extCount; i++)
        {
            var ext = GL.GetStringi(StringName.Extensions, i);
            if (ext is not null)
                glInfo.Extensions.Add(ext);
        }

        glInfo.SupportsKHRDebug = glInfo.Extensions.Contains("GL_KHR_debug");
        glInfo.SupportsDirectStateAccess = glInfo.Extensions.Contains("GL_ARB_direct_state_access");

        glInfo.Vendor = GL.GetString(StringName.Vendor) ?? "";
        glInfo.Renderer = GL.GetString(StringName.Renderer) ?? "";
        glInfo.Version = GL.GetString(StringName.Version) ?? "";
        glInfo.MaxTexture2DSize = GL.GetInteger(GetPName.MaxTextureSize);
        glInfo.MaxTexture2DLayers = GL.GetInteger(GetPName.MaxArrayTextureLayers);
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        ApplyFramebufferSize(e.Width, e.Height);
    }

    private void ApplyFramebufferSize(int width, int height)
    {
        SceneManager.Resize(width, height);
        GL.Viewport(0, 0, width, height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        GL.Enable(EnableCap.Blend);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.ClearColor(.0f, .0f, .0f, 0f);

        SceneManager.Render(new RenderArguments
        {
            Delta = args.Time
        });

        GL.Disable(EnableCap.Blend);
        Context.SwapBuffers();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        MakeCurrent();

        CursorState = CursorState.Normal;
        AssetProvider.Update();
        ThreadRunner.Update();

        var initArguments = new InitArguments
        {
            StartingResolution = ClientSize,
            GLInfo = GLInfo
        };

        lock (_enqueuedEvents)
        {
            // One per frame, not the whole queue: an enqueued event is typically a scene
            // being built, so spreading them keeps a single frame from stalling on all of
            // them at once.
            if (_enqueuedEvents.TryDequeue(out var action))
            {
                action(this);
                // initialize scenes enqueued using game.Enqueue() before anything else runs.
                SceneManager.Initialize(initArguments);
            }
        }

        SceneManager.Initialize(initArguments);

        if (KeyboardState.IsAnyKeyDown)
            SceneManager.Keyboard(KeyboardState);

        SceneManager.Mouse(MouseState, KeyboardState);
        SceneManager.Update(new UpdateArguments
        {
            Delta = args.Time
        });

        if (KeyboardState.IsKeyDown(Keys.LeftControl) && KeyboardState.IsKeyDown(Keys.Q))
            Close();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        SceneManager.TextInput(e);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!e.IsRepeat && IsFullscreenShortcut(e))
        {
            ToggleFullscreen();
            return;
        }

        SceneManager.KeyDown(e);
    }

    /// <summary>
    ///     Alt+Enter on Windows/Linux, Ctrl+Cmd+F on macOS - the platform-native
    ///     "toggle fullscreen" chord in both cases.
    /// </summary>
    private static bool IsFullscreenShortcut(KeyboardKeyEventArgs e)
    {
        if (OperatingSystem.IsMacOS())
            return e.Key == Keys.F &&
                   (e.Modifiers & (KeyModifiers.Super | KeyModifiers.Control)) ==
                   (KeyModifiers.Super | KeyModifiers.Control);

        return (e.Key is Keys.Enter or Keys.KeyPadEnter) && e.Modifiers.HasFlag(KeyModifiers.Alt);
    }

    private void ToggleFullscreen()
    {
        // GLFW's Wayland backend has no way to fullscreen a window on the application's
        // own initiative - the compositor owns window state there - so the assignment
        // below would silently do nothing. Say so instead of swallowing the key.
        if (GLFW.GetPlatform() == Platform.Wayland)
        {
            Logger.Information("[Fullscreen] Refused: {Message}", WaylandFullscreenMessage);
            OnWindowActionUnavailable?.Invoke(WaylandFullscreenMessage);
            return;
        }

        if (WindowState == WindowState.Fullscreen)
        {
            // Restore whatever the window was before, so leaving fullscreen doesn't
            // silently un-maximize a maximized window.
            WindowState = _preFullscreenState;
            return;
        }

        _preFullscreenState = WindowState;
        WindowState = WindowState.Fullscreen;
    }

    protected override void OnFileDrop(FileDropEventArgs e)
    {
        base.OnFileDrop(e);
        if (e.FileNames.Length < 1) return;

        SceneManager.FileDropped(e.FileNames);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        SceneManager.Shutdown();
#if DEBUG
        HotReload.Requested = null;
        _sourceWatcher.Dispose();
#endif
    }

    public void Enqueue(Action<Game> action)
    {
        lock (_enqueuedEvents)
        {
            _enqueuedEvents.Enqueue(action);
        }
    }
}