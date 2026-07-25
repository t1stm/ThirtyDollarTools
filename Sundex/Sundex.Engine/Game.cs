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
        ThreadRunner = new ThreadRunner(Logger);
    }

    public ILogger Logger { get; }
    public Assembly[] AssetAssemblies { get; }
    public AssetProvider AssetProvider { get; }
    public SceneManager SceneManager { get; }
    public ThreadRunner ThreadRunner { get; }

    public GameGlobals Globals { get; } = new();
    private GLInfo GLInfo { get; } = new();

    /// <summary>
    ///     Ratio between physical framebuffer pixels and logical window (client) size.
    ///     Measured directly instead of asking the platform for its DPI/content scale:
    ///     that value doesn't always match reality (e.g. on X11, KDE's "125% scaling" only
    ///     changes Xft.dpi - the framebuffer stays 1:1 with the window - so dividing by a
    ///     reported 1.25 shrank the UI relative to the untouched, unscaled mouse coordinates).
    ///     FramebufferSize / ClientSize is correct on every platform by construction, and
    ///     needs no per-platform (e.g. Wayland) special-casing.
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
        // very first frame (e.g. it can arrive after the window manager settles the real
        // geometry), which left the GL viewport/scenes sized off whatever the driver
        // defaulted to until the user resized the window by hand. Assign it up front too.
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
            while (_enqueuedEvents.TryDequeue(out var action))
            {
                action(this);
                // initialize scenes enqueued using game.Enqueue() and then continue with other enqueued events.
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
        SceneManager.KeyDown(e);
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
    }

    public void Enqueue(Action<Game> action)
    {
        lock (_enqueuedEvents)
        {
            _enqueuedEvents.Enqueue(action);
        }
    }
}