using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Scenes;

namespace ThirtyDollarVisualizer.Helpers.ImGUI;

/// <summary>
/// A basic controller for ImGUI.
/// 
/// Uses code from ImGui.Net_OpenTK_Sample
/// https://github.com/NogginBops/ImGui.NET_OpenTK_Sample
/// </summary>
public class ThirtyDollarImGui
{
    private int OpenGLVersion;
    private readonly ThirtyDollarApplication Application;
    private int Width => Application.Width;
    private int Height => Application.Height;

    private bool KHRDebugAvailable = false;
    private bool CompatibilityProfile;
    
    public ThirtyDollarImGui(ThirtyDollarApplication application)
    {
        Application = application;

        var major_version = GL.GetInteger(GetPName.MajorVersion);
        var minor_version = GL.GetInteger(GetPName.MinorVersion);

        OpenGLVersion = major_version * 100 + minor_version * 10;
        CompatibilityProfile = (GL.GetInteger((GetPName)All.ContextProfileMask) & (int)All.ContextCompatibilityProfileBit) != 0;

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        
        // TODO: Implement.
    }
    
    private void SetKeyMappings()
    {
        var io = ImGui.GetIO();
        io.KeyMap[(int)ImGuiKey.Tab] = (int)Keys.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
        io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
        io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
        io.KeyMap[(int)ImGuiKey.PageUp] = (int)Keys.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown] = (int)Keys.PageDown;
        io.KeyMap[(int)ImGuiKey.Home] = (int)Keys.Home;
        io.KeyMap[(int)ImGuiKey.End] = (int)Keys.End;
        io.KeyMap[(int)ImGuiKey.Delete] = (int)Keys.Delete;
        io.KeyMap[(int)ImGuiKey.Backspace] = (int)Keys.Backspace;
        io.KeyMap[(int)ImGuiKey.Enter] = (int)Keys.Enter;
        io.KeyMap[(int)ImGuiKey.Escape] = (int)Keys.Escape;
        io.KeyMap[(int)ImGuiKey.A] = (int)Keys.A;
        io.KeyMap[(int)ImGuiKey.C] = (int)Keys.C;
        io.KeyMap[(int)ImGuiKey.V] = (int)Keys.V;
        io.KeyMap[(int)ImGuiKey.X] = (int)Keys.X;
        io.KeyMap[(int)ImGuiKey.Y] = (int)Keys.Y;
        io.KeyMap[(int)ImGuiKey.Z] = (int)Keys.Z;
    }

    public void Update(ThirtyDollarApplication application)
    {
    }

    public void Render(ThirtyDollarApplication application)
    {
    }
}