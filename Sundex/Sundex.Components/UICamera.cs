using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Cameras;

namespace Sundex.Components;

/// <summary>A top-left, pixel-for-pixel camera for a UI: what a UIContext renders through.</summary>
public sealed class UICamera(Vector3 position, Vector2i viewport) : Camera(position, viewport);
