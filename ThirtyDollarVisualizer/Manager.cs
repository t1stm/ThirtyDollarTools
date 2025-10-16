using System.ComponentModel;
using System.Reflection;
using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Base_Objects.Text;
using ThirtyDollarVisualizer.Helpers.Logging;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Attributes;
using ThirtyDollarVisualizer.Renderer.Shaders;
using ThirtyDollarVisualizer.Scenes;
using ThirtyDollarVisualizer.Settings;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace ThirtyDollarVisualizer;

public class Manager(int width, int height, string title, int? fps = null, WindowIcon? icon = null)
    : GameWindow(new GameWindowSettings
        {
            UpdateFrequency = fps ?? 0
        },
        new NativeWindowSettings
        {
            AutoIconify = false,
            ClientSize = (width, height),
            Title = title,
            Icon = icon,
            Vsync = fps == null ? VSyncMode.On : VSyncMode.Off,
            TransparentFramebuffer = SettingsHandler.Settings.TransparentFramebuffer
        })
{
    public static Manager? Instance { get; private set; }
    
    public readonly SemaphoreSlim RenderBlock = new(1);
    public readonly List<IScene> Scenes = [];

    public void ToggleFullscreen()
    {
        WindowState = WindowState switch
        {
            WindowState.Fullscreen => WindowState.Normal,
            _ => WindowState.Fullscreen
        };
    }

    protected override void OnLoad()
    {
        Instance = this;
        
        base.OnLoad();
        SetGLInfo();
        
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.Multisample);
        
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        if (GLInfo.SupportsKHRDebug)
            GL.DebugMessageCallback(DebugCallback, in IntPtr.Zero);
        
        Fonts.Initialize();

        GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
        GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

        ReflectionPreloadObjects();
        
        foreach (var scene in Scenes) scene.Init(this);

        foreach (var scene in Scenes) scene.Start();
    }

    private static void ReflectionPreloadObjects()
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<PreloadGLAttribute>() != null);

        foreach (var type in types)
        {
            if (!typeof(IGLPreloadable).IsAssignableFrom(type))
                continue;
            
            var method = type.GetMethod("Preload");
            if (method is null)
                throw new Exception("Method not found");
            
            method.Invoke(null, null);
        }

    }

    private static unsafe void DebugCallback(DebugSource source, DebugType type, uint id, 
        DebugSeverity severity, int length, IntPtr messagePtr, IntPtr userParameter)
    {
        if (type == DebugType.DebugTypeOther) return;
        
        var stringFromPointer = new ReadOnlySpan<byte>(messagePtr.ToPointer(), length);
        var sourceText = source != DebugSource.DontCare ? source.ToString()[11..] : "Unknown";
        var typeText = type != DebugType.DontCare ? type.ToString()[9..] : "Unknown";
        var severityText = severity != DebugSeverity.DontCare ? severity.ToString()[13..] : "Unknown";
        
        DefaultLogger.Log($"OpenGL {sourceText}", $"({typeText}, {id}) {severityText}: {Encoding.ASCII.GetString(stringFromPointer)}");
    }

    private static ulong _errorID;
    public static void CheckErrors(ReadOnlySpan<char> context)
    {
        ErrorCode err;
        while ((err = GL.GetError()) != ErrorCode.NoError)
            DefaultLogger.Log("OpenGL Generic", $"({context}): [{++_errorID}] {err}");
    }

    private static void SetGLInfo()
    {
        GLInfo.Vendor = GL.GetString(StringName.Vendor) ?? "";
        GLInfo.Renderer = GL.GetString(StringName.Renderer) ?? "";
        GLInfo.Version = GL.GetString(StringName.Version) ?? "";
        
        GLInfo.MaxTexture2DSize = GL.GetInteger(GetPName.MaxTextureSize);
        GLInfo.MaxTexture2DLayers = GL.GetInteger(GetPName.MaxArrayTextureLayers);

        var ext_count = GL.GetInteger(GetPName.NumExtensions);
        GLInfo.Extensions.EnsureCapacity(ext_count);
        for (uint i = 0; i < ext_count; i++)
        {
            var ext = GL.GetStringi(StringName.Extensions, i);
            if (ext is not null)
                GLInfo.Extensions.Add(ext);
        }

        GLInfo.SupportsKHRDebug = GLInfo.Extensions.Contains("GL_KHR_debug");
    }
    
    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        foreach (var scene in Scenes) scene.Resize(e.Width, e.Height);
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        Context.MakeCurrent();
        RenderBlock.Wait();

        GL.Enable(EnableCap.Blend);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.ClearColor(.0f, .0f, .0f, 0f);

        foreach (var scene in Scenes) scene.Render();

        GL.Disable(EnableCap.Blend);
        Context.SwapBuffers();
        RenderBlock.Release();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    { 
        MakeCurrent();
        ShaderPool.UploadShadersToPreload();
        
        if (KeyboardState.IsAnyKeyDown)
            foreach (var scene in Scenes)
                scene.Keyboard(KeyboardState);

        foreach (var scene in Scenes) scene.Mouse(MouseState, KeyboardState);
        foreach (var scene in Scenes) scene.Update();

#if DEBUG
        // reload shaders when in debug
        if (KeyboardState.IsKeyPressed(Keys.F2) && KeyboardState.IsKeyDown(Keys.LeftControl))
        {
            ShaderPool.Reload();
            DefaultLogger.Log("Manager", "Reloaded all shaders.");
        }
#endif
        
        if (!KeyboardState.IsKeyDown(Keys.Escape)) return;

        foreach (var scene in Scenes) scene.Close();
        Close();
    }

    protected override void OnFileDrop(FileDropEventArgs e)
    {
        base.OnFileDrop(e);
        if (e.FileNames.Length < 1) return;

        foreach (var scene in Scenes)
            scene.FileDrop(e.FileNames);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        foreach (var scene in Scenes) scene.Close();
    }
}