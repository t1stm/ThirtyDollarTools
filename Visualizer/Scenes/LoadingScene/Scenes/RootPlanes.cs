using Sundex.Components.Abstractions;
using Sundex.Components.Color_Scheme;
using OpenTK.Mathematics;
using Shared.Animations;
using Shared.Renderer;
using Shared.Renderer.Planes;
using Sundex.Core.Animations;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Enums;

namespace LoadingScene.Scenes;

public static class RootPlanes
{
    public static AnimatedPlane<Renderable>[] GenerateRootPlanes(UIContext context)
    {
        var viewportX = context.ViewportWidth;
        var viewportY = context.ViewportHeight;

        const float scaleFactor = 4f;

        var g1 = GradientPlaneDefinitions.NewAccentBlueRadial();
        var g2 = GradientPlaneDefinitions.NewMagentaBlueRadial();
        var g3 = GradientPlaneDefinitions.NewTealToBgSurfaceRadial();

        var g4 = GradientPlaneDefinitions.NewAccentBlueRadial();
        var g5 = GradientPlaneDefinitions.NewMagentaBlueRadial();
        var g6 = GradientPlaneDefinitions.NewTealToBgSurfaceRadial();

        const SteppingFunction initialSteppingFunction = SteppingFunction.ExpoOut;
        const SteppingFunction finalSteppingFunction = SteppingFunction.CubicInOut;

        AnimatedPlane<Renderable>[] animations =
        [
            new(g1, new KeyframedAnimation([
                new Keyframe
                {
                    Position = new Vector3(-viewportX / 2f, -viewportY / 2f, 0),
                    LengthMs = 7500,
                    SteppingFunction = initialSteppingFunction
                },
                new Keyframe
                {
                    Position = new Vector3(-75f * scaleFactor, -45f * scaleFactor, 0),
                    LengthMs = 15000,
                    Scale = new Vector3(1.1f, 1.1f, 1f),
                    BezierP1 = new Vector3(75f * scaleFactor, 75f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.LoopStart,
                    SteppingFunction = finalSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(75f * scaleFactor, 45f * scaleFactor, 0),
                    LengthMs = 15000,
                    Scale = new Vector3(1f, 1f, 1f), BezierP1 = new Vector3(-75f * scaleFactor, -75f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.Invert,
                    SteppingFunction = finalSteppingFunction,
                }
            ])),

            new(g2, new KeyframedAnimation([
                new Keyframe
                {
                    Position = new Vector3(0, -viewportY / 2f, 0),
                    LengthMs = 7500,
                    SteppingFunction = initialSteppingFunction
                },
                new Keyframe
                {
                    Position = new Vector3(60f * scaleFactor, 90f * scaleFactor, 0),
                    LengthMs = 20000,
                    Rotation = 0.2f, BezierP1 = new Vector3(-120f * scaleFactor, 0, 0),
                    LoopingMode = AnimationLoopingMode.LoopStart,
                    SteppingFunction = finalSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(-60f * scaleFactor, -90f * scaleFactor, 0),
                    LengthMs = 20000,
                    Rotation = -0.2f, BezierP1 = new Vector3(120f * scaleFactor, 0, 0),
                    LoopingMode = AnimationLoopingMode.Invert,
                    SteppingFunction = finalSteppingFunction,
                }
            ])),

            new(g3, new KeyframedAnimation([
                new Keyframe
                {
                    Position = new Vector3(viewportX / 2f, 0, 0),
                    LengthMs = 7500,
                    SteppingFunction = initialSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(90f * scaleFactor, -60f * scaleFactor, 0),
                    LengthMs = 18000,
                    Opacity = 0.4f, BezierP1 = new Vector3(0, 150f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.LoopStart,
                    SteppingFunction = finalSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(-90f * scaleFactor, 60f * scaleFactor, 0),
                    LengthMs = 18000,
                    Opacity = 0.7f, BezierP1 = new Vector3(0, -150f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.Invert,
                    SteppingFunction = finalSteppingFunction,
                }
            ])),

            new(g4, new KeyframedAnimation([
                new Keyframe
                {
                    Position = new Vector3(0, viewportY / 2f, 0),
                    LengthMs = 7500,
                    SteppingFunction = initialSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(-105f * scaleFactor, 75f * scaleFactor, 0),
                    LengthMs = 17000,
                    Scale = new Vector3(0.9f, 0.9f, 1f),
                    BezierP1 = new Vector3(-45f * scaleFactor, -150f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.LoopStart,
                    SteppingFunction = finalSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(105f * scaleFactor, -75f * scaleFactor, 0),
                    LengthMs = 17000,
                    Scale = new Vector3(1.05f, 1.05f, 1f),
                    BezierP1 = new Vector3(45f * scaleFactor, 150f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.Invert,
                    SteppingFunction = finalSteppingFunction,
                }
            ])),

            new(g5, new KeyframedAnimation([
                new Keyframe
                {
                    Position = new Vector3(0, -viewportY / 2f, 0),
                    LengthMs = 7500,
                    SteppingFunction = initialSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(-120f * scaleFactor, -90f * scaleFactor, 0),
                    LengthMs = 22000,
                    Rotation = -0.4f, BezierP1 = new Vector3(0, 225f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.LoopStart,
                    SteppingFunction = finalSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(120f * scaleFactor, 90f * scaleFactor, 0),
                    LengthMs = 22000,
                    Rotation = 0.1f, BezierP1 = new Vector3(0, -225f * scaleFactor, 0),
                    LoopingMode = AnimationLoopingMode.Invert,
                    SteppingFunction = finalSteppingFunction,
                }
            ])),

            new(g6, new KeyframedAnimation([
                new Keyframe
                {
                    Position = new Vector3(0, viewportY / 2f, 0),
                    LengthMs = 7500,
                    SteppingFunction = initialSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(75f * scaleFactor, -120f * scaleFactor, 0),
                    LengthMs = 19000,
                    Opacity = 0.5f, BezierP1 = new Vector3(-180f * scaleFactor, 0, 0),
                    LoopingMode = AnimationLoopingMode.LoopStart,
                    SteppingFunction = finalSteppingFunction,
                },
                new Keyframe
                {
                    Position = new Vector3(-75f * scaleFactor, 120f * scaleFactor, 0),
                    LengthMs = 19000,
                    Opacity = 0.8f, BezierP1 = new Vector3(180f * scaleFactor, 0, 0),
                    LoopingMode = AnimationLoopingMode.Invert,
                    SteppingFunction = finalSteppingFunction,
                }
            ]))
        ];

        PositionGradients(animations, viewportX, viewportY);
        return animations;
    }

    public static void PositionGradients(AnimatedPlane<Renderable>[] gradients, float viewportWidth,
        float viewportHeight)
    {
        if (gradients.Length < 6) return;

        var scaleFactor = Math.Min(viewportWidth / 1920f, viewportHeight / 1080f);

        // Hardcoded positions at the borders and corners
        var positions = new[]
        {
            new Vector3(0, 0, 0), // Top-left
            new Vector3(viewportWidth, 0, 0), // Top-right
            new Vector3(0, viewportHeight, 0), // Bottom-left
            new Vector3(viewportWidth, viewportHeight, 0), // Bottom-right
            new Vector3(viewportWidth / 2f, 0, 0), // Top-center
            new Vector3(viewportWidth / 2f, viewportHeight, 0) // Bottom-center
        };

        for (var i = 0; i < gradients.Length; i++)
        {
            var gradient = gradients[i].Renderable;
            var baseScale = 1100f * scaleFactor;
            
            gradient.Scale = new Vector3(baseScale, baseScale, 1);
            gradient.SetPosition(positions[i], PositionAlign.Center);
        }
    }
}