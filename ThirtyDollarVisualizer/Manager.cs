using System.ComponentModel;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects.Text;
using ThirtyDollarVisualizer.Scenes;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace ThirtyDollarVisualizer;

public class Manager(int width, int height, string title, int? fps = null, WindowIcon? icon = null)
    : GameWindow(new GameWindowSettings
        {
            UpdateFrequency = fps ?? 0
        },
        new NativeWindowSettings
        {
            ClientSize = (width, height),
            Title = title,
            APIVersion = new Version(3, 2),
            Icon = icon,
            Vsync = fps == null ? VSyncMode.On : VSyncMode.Off
        })
{
    public readonly SemaphoreSlim RenderBlock = new(1);
    public readonly List<IScene> Scenes = new();

    public static void CheckErrors()
    {
        ErrorCode errorCode;
        while ((errorCode = GL.GetError()) != ErrorCode.NoError)
            Console.WriteLine($"[OpenGL Error]: (0x{(int)errorCode:x8}) \'{errorCode}\'");
    }

    protected override void OnLoad()
    {
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.Multisample);

        CheckErrors();
        base.OnLoad();

        Fonts.Initialize();

        foreach (var scene in Scenes) scene.Init(this);

        foreach (var scene in Scenes) scene.Start();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        foreach (var scene in Scenes) scene.Resize(e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        RenderBlock.Wait();

        GL.Enable(EnableCap.Blend);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.ClearColor(.0f, .0f, .0f, 1f);

        foreach (var scene in Scenes) scene.Render();

        GL.Disable(EnableCap.Blend);
        Context.SwapBuffers();
        RenderBlock.Release();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        foreach (var scene in Scenes) scene.Update();

        if (KeyboardState.IsAnyKeyDown)
            foreach (var scene in Scenes)
                scene.Keyboard(KeyboardState);

        foreach (var scene in Scenes) scene.Mouse(MouseState, KeyboardState);

        if (!KeyboardState.IsKeyDown(Keys.Escape)) return;

        foreach (var scene in Scenes) scene.Close();
        Close();
    }

    protected override void OnFileDrop(FileDropEventArgs e)
    {
        base.OnFileDrop(e);
        if (e.FileNames.Length < 1) return;

        foreach (var scene in Scenes)
        foreach (var file in e.FileNames)
            scene.FileDrop(file);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        foreach (var scene in Scenes) scene.Close();
    }
}