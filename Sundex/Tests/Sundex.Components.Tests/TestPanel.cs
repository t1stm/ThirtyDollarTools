using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class TestPanel(UIContext context) : Panel(context)
{
    public string CustomTag { get; set; } = "panel";
    public override string Tag => CustomTag;

    public new int Index
    {
        get => base.Index;
        set => base.Index = value;
    }

    public void TestHandleRenderableSwap(object? oldV, object? newV, string? propName = null)
    {
        HandleRenderableSwap(oldV, newV, propName);
    }
}