using System.ComponentModel;
using System.Reflection;
using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Serilog.Core;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Scenes;
using ThirtyDollarVisualizer.Engine.Scenes.Arguments;

namespace ThirtyDollarVisualizer.Engine;

public class Game : GameWindow
{
    public readonly Logger Logger;
    private GLDebugProc _storedDebugCallback = null!; // exists due to .NET design

    public Game(Assembly externalAssetAssembly, GameWindowSettings gameSettings,
        NativeWindowSettings nativeWindowSettings) :
        base(gameSettings, nativeWindowSettings)
    {
        var serilogLogger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "{Level:u3}: {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Debug()
            .CreateLogger();

        Logger = serilogLogger;

        var callingAssembly = Assembly.GetExecutingAssembly();
        if (ExternalAssetAssembly == callingAssembly)
            throw new Exception("Asset Assembly cannot be the calling assembly.");

        ExternalAssetAssembly = externalAssetAssembly;
        AssetProvider = new AssetProvider(Logger, [callingAssembly, ExternalAssetAssembly]);
        SceneManager = new SceneManager(Logger, AssetProvider);
    }

    public Assembly ExternalAssetAssembly { get; }
    public AssetProvider AssetProvider { get; }
    public SceneManager SceneManager { get; }
    private GLInfo GLInfo { get; set; } = new();
    private Queue<Action<Game>> _enqueuedEvents = new();

    protected override void OnLoad()
    {
        base.OnLoad();
        GLInfo = GetGLInfo();

        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.Multisample);

        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        // .NET GC automatically collects this unless it's stored somewhere in a class.
        // See: 
        _storedDebugCallback = DebugCallback;

        if (GLInfo.SupportsKHRDebug)
            GL.DebugMessageCallback(_storedDebugCallback, in IntPtr.Zero);
        else RenderMarker.Enabled = false;

        GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
        GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

        RenderMarker.Debug("Game Window Initialized");

        ReflectionPreloadObjects(Assembly.GetExecutingAssembly()); // preload engine stuff first
        ReflectionPreloadObjects(ExternalAssetAssembly);

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

        Logger.Debug("[OpenGL]: {sourceText}, ({typeText}, {id}) {severityText}: {callbackMessage}",
            sourceText, typeText, id, severityText, stringBuffer.ToString());
    }

    private static GLInfo GetGLInfo()
    {
        var gl_info = new GLInfo
        {
            Vendor = GL.GetString(StringName.Vendor) ?? "",
            Renderer = GL.GetString(StringName.Renderer) ?? "",
            Version = GL.GetString(StringName.Version) ?? "",
            MaxTexture2DSize = GL.GetInteger(GetPName.MaxTextureSize),
            MaxTexture2DLayers = GL.GetInteger(GetPName.MaxArrayTextureLayers)
        };

        var ext_count = GL.GetInteger(GetPName.NumExtensions);
        gl_info.Extensions.EnsureCapacity(ext_count);

        for (uint i = 0; i < ext_count; i++)
        {
            var ext = GL.GetStringi(StringName.Extensions, i);
            if (ext is not null)
                gl_info.Extensions.Add(ext);
        }

        gl_info.SupportsKHRDebug = gl_info.Extensions.Contains("GL_KHR_debug");

        return gl_info;
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        SceneManager.Resize(e.Width, e.Height);
        GL.Viewport(0, 0, e.Width, e.Height);
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
        AssetProvider.Update();
        
        lock (_enqueuedEvents)
            while (_enqueuedEvents.TryDequeue(out var action)) 
                action(this);
        
        SceneManager.Initialize(new InitArguments
        {
            StartingResolution = ClientSize,
            GLInfo = GLInfo
        });

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
            _enqueuedEvents.Enqueue(action);
    }
}