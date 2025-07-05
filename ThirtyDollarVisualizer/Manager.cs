using System.ComponentModel;
using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Base_Objects.Text;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Helpers.Logging;
using ThirtyDollarVisualizer.Renderer.Shaders;
using ThirtyDollarVisualizer.Scenes;
using ThirtyDollarVisualizer.Settings;

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
        SetGLInfo();
        
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.Multisample);
        
        GL.Enable(EnableCap.DebugOutput);
        if (GLInfo.Extensions.Contains("GL_KHR_debug"))
            GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
        
        base.OnLoad();
        Fonts.Initialize();

        GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
        GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

        foreach (var scene in Scenes) scene.Init(this);

        foreach (var scene in Scenes) scene.Start();
    }

    private static unsafe void DebugCallback(DebugSource source, DebugType type, int id, 
        DebugSeverity severity, int length, IntPtr messagePtr, IntPtr userParameter)
    {
        if (type == DebugType.DebugTypeOther) return;
        
        var stringFromPointer = new ReadOnlySpan<byte>(messagePtr.ToPointer(), length);
        var sourceText = source != DebugSource.DontCare ? source.ToString()[11..] : "Unknown";
        var typeText = type != DebugType.DontCare ? type.ToString()[9..] : "Unknown";
        var severityText = severity != DebugSeverity.DontCare ? severity.ToString()[13..] : "Unknown";
        
        DefaultLogger.Log($"OpenGL {sourceText}", $"({typeText}, {id}) {severityText}: {Encoding.ASCII.GetString(stringFromPointer)}");
    }

    private static void SetGLInfo()
    {
        GLInfo.Vendor = GL.GetString(StringName.Vendor);
        GLInfo.Renderer = GL.GetString(StringName.Renderer);
        GLInfo.Version = GL.GetString(StringName.Version);
        
        GLInfo.MaxTexture2DSize = GL.GetInteger(GetPName.MaxTextureSize);
        GLInfo.MaxTexture2DLayers = GL.GetInteger(GetPName.MaxArrayTextureLayers);

        var ext_count = GL.GetInteger(GetPName.NumExtensions);
        GLInfo.Extensions.EnsureCapacity(ext_count);
        for (var i = 0; i < ext_count; i++)
        {
            var ext = GL.GetString(StringNameIndexed.Extensions, i);
            GLInfo.Extensions.Add(ext);
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        foreach (var scene in Scenes) scene.Resize(e.Width, e.Height);
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
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