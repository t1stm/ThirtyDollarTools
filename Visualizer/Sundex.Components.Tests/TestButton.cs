using Sundex.Components.Abstractions;
using Sundex.Components.Labels;

namespace Sundex.Components.Tests;

public class TestButton(UIContext context, string label) : Button(context, label)
{
    public int GetIndex()
    {
        return Index;
    }

    public void TestHandleRenderableSwap(object? oldV, object? newV, string? propName = null)
    {
        HandleRenderableSwap(oldV, newV, propName);
    }
}