using System.Numerics;
using ImGuiNET;

namespace ThirtyDollarVisualizer.Helpers.ImGUI;

public static class ImGUIStyle
{
    public static void SetupStyle()
    {
        // Comfy style by Giuseppe from ImThemes
        var style = ImGui.GetStyle();

        style.Alpha = 1.0f;
        style.DisabledAlpha = 0.1000000014901161f;
        style.WindowPadding = new Vector2(8.0f, 8.0f);
        style.WindowRounding = 10.0f;
        style.WindowBorderSize = 0.0f;
        style.WindowMinSize = new Vector2(30.0f, 30.0f);
        style.WindowTitleAlign = new Vector2(0.5f, 0.5f);
        style.WindowMenuButtonPosition = ImGuiDir.Right;
        style.ChildRounding = 5.0f;
        style.ChildBorderSize = 1.0f;
        style.PopupRounding = 10.0f;
        style.PopupBorderSize = 0.0f;
        style.FramePadding = new Vector2(5.0f, 3.5f);
        style.FrameRounding = 5.0f;
        style.FrameBorderSize = 0.0f;
        style.ItemSpacing = new Vector2(5.0f, 4.0f);
        style.ItemInnerSpacing = new Vector2(5.0f, 5.0f);
        style.CellPadding = new Vector2(4.0f, 2.0f);
        style.IndentSpacing = 5.0f;
        style.ColumnsMinSpacing = 5.0f;
        style.ScrollbarSize = 15.0f;
        style.ScrollbarRounding = 9.0f;
        style.GrabMinSize = 15.0f;
        style.GrabRounding = 5.0f;
        style.TabRounding = 5.0f;
        style.TabBorderSize = 0.0f;
        style.TabMinWidthForCloseButton = 0.0f;
        style.ColorButtonPosition = ImGuiDir.Right;
        style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
        style.SelectableTextAlign = new Vector2(0.0f, 0.0f);

        style.Colors[(int)ImGuiCol.Text] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(1.0f, 1.0f, 1.0f, 0.3605149984359741f);
        style.Colors[(int)ImGuiCol.WindowBg] =
            new Vector4(0.09803921729326248f, 0.09803921729326248f, 0.09803921729326248f, 1.0f);
        style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.PopupBg] =
            new Vector4(0.09803921729326248f, 0.09803921729326248f, 0.09803921729326248f, 1.0f);
        style.Colors[(int)ImGuiCol.Border] = new Vector4(0.4235294163227081f, 0.3803921639919281f, 0.572549045085907f,
            0.54935622215271f);
        style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.FrameBg] =
            new Vector4(0.1568627506494522f, 0.1568627506494522f, 0.1568627506494522f, 1.0f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.3803921639919281f, 0.4235294163227081f,
            0.572549045085907f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.6196078658103943f, 0.5764706134796143f,
            0.7686274647712708f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.TitleBg] =
            new Vector4(0.09803921729326248f, 0.09803921729326248f, 0.09803921729326248f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBgActive] =
            new Vector4(0.09803921729326248f, 0.09803921729326248f, 0.09803921729326248f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBgCollapsed] =
            new Vector4(0.2588235437870026f, 0.2588235437870026f, 0.2588235437870026f, 0.0f);
        style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.ScrollbarBg] =
            new Vector4(0.1568627506494522f, 0.1568627506494522f, 0.1568627506494522f, 0.0f);
        style.Colors[(int)ImGuiCol.ScrollbarGrab] =
            new Vector4(0.1568627506494522f, 0.1568627506494522f, 0.1568627506494522f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] =
            new Vector4(0.2352941185235977f, 0.2352941185235977f, 0.2352941185235977f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabActive] =
            new Vector4(0.294117659330368f, 0.294117659330368f, 0.294117659330368f, 1.0f);
        style.Colors[(int)ImGuiCol.CheckMark] =
            new Vector4(0.294117659330368f, 0.294117659330368f, 0.294117659330368f, 1.0f);
        style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.6196078658103943f, 0.5764706134796143f,
            0.7686274647712708f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.8156862854957581f, 0.772549033164978f,
            0.9647058844566345f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.6196078658103943f, 0.5764706134796143f, 0.7686274647712708f,
            0.5490196347236633f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.8156862854957581f, 0.772549033164978f,
            0.9647058844566345f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.Header] = new Vector4(0.6196078658103943f, 0.5764706134796143f, 0.7686274647712708f,
            0.5490196347236633f);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.8156862854957581f, 0.772549033164978f,
            0.9647058844566345f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.6196078658103943f, 0.5764706134796143f,
            0.7686274647712708f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.8156862854957581f, 0.772549033164978f,
            0.9647058844566345f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.6196078658103943f, 0.5764706134796143f,
            0.7686274647712708f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.8156862854957581f, 0.772549033164978f,
            0.9647058844566345f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.6196078658103943f, 0.5764706134796143f, 0.7686274647712708f,
            0.5490196347236633f);
        style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.TabActive] = new Vector4(0.8156862854957581f, 0.772549033164978f,
            0.9647058844566345f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.0f, 0.4509803950786591f, 1.0f, 0.0f);
        style.Colors[(int)ImGuiCol.TabUnfocusedActive] =
            new Vector4(0.1333333402872086f, 0.2588235437870026f, 0.4235294163227081f, 0.0f);
        style.Colors[(int)ImGuiCol.PlotLines] =
            new Vector4(0.294117659330368f, 0.294117659330368f, 0.294117659330368f, 1.0f);
        style.Colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.6196078658103943f, 0.5764706134796143f,
            0.7686274647712708f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.TableHeaderBg] =
            new Vector4(0.1882352977991104f, 0.1882352977991104f, 0.2000000029802322f, 1.0f);
        style.Colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.4235294163227081f, 0.3803921639919281f,
            0.572549045085907f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.4235294163227081f, 0.3803921639919281f,
            0.572549045085907f, 0.2918455004692078f);
        style.Colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1.0f, 1.0f, 1.0f, 0.03433477878570557f);
        style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.7372549176216125f, 0.6941176652908325f,
            0.886274516582489f, 0.5490196347236633f);
        style.Colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.0f, 1.0f, 0.0f, 0.8999999761581421f);
        style.Colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        style.Colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.0f, 1.0f, 1.0f, 0.699999988079071f);
        style.Colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.800000011920929f, 0.800000011920929f,
            0.800000011920929f, 0.2000000029802322f);
        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.800000011920929f, 0.800000011920929f,
            0.800000011920929f, 0.3499999940395355f);
    }
}