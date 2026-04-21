using OpenTK.Mathematics;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Markup;

var context = new SundexContext(new UIContext
{
    Camera = new DollarStoreCamera(new Vector3(0, 0, 0), new Vector2i(1920, 1080))
});

var component =
    context.NewComponent(File.ReadAllText(
        "/home/kris/RiderProjects/ThirtyDollarTools/Visualizer/Engine/Sundex.Markup/EXAMPLE.snx.xml"));

if (component is null) throw new InvalidOperationException("Component creation failed");