using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Uniforms;

namespace Sundex.Components.Color_Scheme;

public static class GradientPlaneDefinitions
{
    public static GradientPlane NewAccentBlueRadial()
    {
        var opacity = new Vector4(1, 1, 1, 0.25f);
        return new GradientPlane
        {
            GradientType = GradientType.Radial,
            GradientColors =
            [
                DarkScheme.AccentBlue * opacity,
                DarkScheme.BlueDark * opacity,
                new Vector4(0, 0, 0, 0)
            ],
            GradientStops = [0f, 0.2f, 1]
        };
    }

    public static GradientPlane NewMagentaBlueRadial()
    {
        var opacity = new Vector4(1, 1, 1, 0.20f);
        return new GradientPlane
        {
            GradientType = GradientType.Radial,
            GradientColors =
            [
                DarkScheme.AccentMagenta * opacity,
                DarkScheme.AccentBlue * opacity,
                new Vector4(0, 0, 0, 0)
            ],
            GradientStops = [0f, 0.15f, 1],
            BorderRadius = 50f
        };
    }

    public static GradientPlane NewTealToBgSurfaceRadial()
    {
        var opacity = new Vector4(1, 1, 1, 0.18f);
        return new GradientPlane
        {
            GradientType = GradientType.Radial,
            GradientColors =
            [
                DarkScheme.AccentTeal * opacity,
                DarkScheme.BgSurface * opacity,
                new Vector4(0, 0, 0, 0)
            ],
            GradientStops = [0f, 0.7f, 1f],
            BorderRadius = 50f
        };
    }
}