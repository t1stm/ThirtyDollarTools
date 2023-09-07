using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Scenes;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace ThirtyDollarVisualizer;

public class Manager : GameWindow
{
    private readonly List<IScene> Scenes = new();

    public Manager(int width, int height, string title, string? composition_location = null) : base(new GameWindowSettings
        {
            UpdateFrequency = 1000
        },
        new NativeWindowSettings { Size = (width, height), Title = title })
    {
        Scenes.Add(new ThirtyDollarApplication(width, height, composition_location));
    }

    public static void CheckErrors()
    {
        ErrorCode errorCode;
        while ((errorCode = GL.GetError()) != ErrorCode.NoError)
            Console.WriteLine($"[OpenGL Error]: (0x{(int)errorCode:x8}) \'{errorCode}\'");
    }

    protected override void OnLoad()
    {
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        CheckErrors();
        base.OnLoad();

        foreach (var scene in Scenes)
        {
            scene.Init();
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        foreach (var scene in Scenes)
        {
            scene.Resize(e.Width, e.Height);
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    { 
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.ClearColor(.0f, .0f, .0f, 1f);

        foreach (var scene in Scenes)
        {
            scene.Render();
        }

        SwapBuffers();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        foreach (var scene in Scenes)
        {
            Task.Run(() =>
            {
                scene.Update();
            });
        }
    }

    public override void Close()
    {
        AudioContext.Destroy();
        base.Close();
    }
}