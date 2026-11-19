using Sundex.Core.Renderer.Planes;
using Sundex.Core.Renderer.Planes.Uniforms;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Components.Renderer.Planes.Extensions;

public static class GradientPlaneExtensions
{
    public static GradientPlane GenerateGradientPlane(this GradientValue gradient)
    {
        return new GradientPlane
        {
            GradientType = gradient.Type switch
            {
                "radial" => GradientType.Radial,
                "conical" => GradientType.Conical,
                "solid" => GradientType.Solid,
                _ => GradientType.Linear // also matches linear
            },
            GradientStops = [.. gradient.Stops.Select(stop => stop.Percentage)],
            GradientColors = [.. gradient.Stops.Select(stop => stop.Color.Vector)]
        };
    }
}