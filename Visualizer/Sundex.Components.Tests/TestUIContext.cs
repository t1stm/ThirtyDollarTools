using System.Diagnostics.CodeAnalysis;
using Sundex.Components.Abstractions;
using OpenTK.Mathematics;
using Shared;

namespace Sundex.Components.Tests;

public class TestUIContext : UIContext
{
    [SetsRequiredMembers]
    public TestUIContext()
    {
        Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
    }
}
