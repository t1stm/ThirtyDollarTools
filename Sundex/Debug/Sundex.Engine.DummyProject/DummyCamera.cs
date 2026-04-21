using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Cameras;

namespace Sundex.Engine.DummyProject;

public class DummyCamera(Vector3 position, Vector2i viewport) : Camera(position, viewport);