using Shared.Renderer.Planes.Uniforms;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Shared.Renderer.Planes.Extensions;

public static class GradientPlaneExtensions
{
    public static GradientPlane GenerateGradientPlane(this GradientValue gv)
    {
        return new GradientPlane
        {
            GradientType = gv.Type switch
            {
                "radial" => GradientType.Radial,
                "conical" => GradientType.Conical,
                "solid" => GradientType.Solid,
                _ => GradientType.Linear // also matches linear
            },
            GradientStops = gv.Stops.Select(stop => stop.Percentage).ToList(),
            GradientColors = gv.Stops.Select(stop => stop.Color.Vector).ToList()
        };
    }
}