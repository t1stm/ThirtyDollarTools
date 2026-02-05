using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;

namespace ThirtyDollarVisualizer.Engine.DummyProject;

public class DummyCamera(Vector3 position, Vector2i viewport) : Camera(position, viewport);