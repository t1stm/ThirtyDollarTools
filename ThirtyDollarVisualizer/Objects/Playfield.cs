using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Playfield(Vector2i viewport)
{
    private readonly DollarStoreCamera _camera = new((0, 0, 0), viewport);
    private Vector3 _position;
    private Vector2 _scale;
    private Vector2i _viewport = viewport;

    public float X
    {
        get => _position.X;
        set => _position.X = value;
    }
    
    public float Y
    {
        get => _position.Y;
        set => _position.Y = value;
    }
    
    public float Z
    {
        get => _position.Z;
        set => _position.Z = value;
    }

    public Vector3 Position
    {
        get => _position;
        set => _position = value;
    }
    
    public float Width
    {
        get => _scale.X;
        set => _scale.X = value;
    }
    
    public float Height
    {
        get => _scale.Y;
        set => _scale.Y = value;
    }

    public void HandleViewportUpdate(Vector2i viewport)
    {
        _viewport = viewport;
        _camera.Viewport = viewport;
        _camera.UpdateMatrix();
    }

    public void Render()
    {
        if (_camera.Viewport != _viewport) HandleViewportUpdate(_viewport);
        
        
    }
}