using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;

namespace ThirtyDollarVisualizer.Objects;

public sealed class DollarStoreCamera : Camera
{
    private readonly float _scrollSpeed;
    private PulseAnimation? _pulseAnimation;
    private Vector3 _offset = (0, 0, 0);
    private Vector3 _virtualPosition;
    public Action<float>? OnZoom = null;

    public DollarStoreCamera(Vector3 virtualPosition, Vector2i viewport, float scrollSpeed = 7.5f) : base(
        virtualPosition, viewport)
    {
        _virtualPosition = virtualPosition;
        _scrollSpeed = scrollSpeed;
        UpdateMatrix();
    }

    public bool IsOutsideOfCameraView(Vector3 position, Vector3 scale, float marginFromTopBottom = 0)
    {
        var collide_top = position.Y < _virtualPosition.Y + marginFromTopBottom;
        var collide_bottom = position.Y + scale.Y > _virtualPosition.Y + Height - marginFromTopBottom;

        var collide_left = position.X < _virtualPosition.X;
        var collide_right = position.X + scale.X > _virtualPosition.X + Width;

        return collide_top || collide_bottom || collide_left || collide_right;
    }

    public void ScrollTo(Vector3 position)
    {
        _virtualPosition = position;
    }

    public void SetPosition(Vector3 position)
    {
        _virtualPosition = position;
        InnerPosition = position;
    }

    public void ScrollDelta(Vector3 delta)
    {
        _virtualPosition += delta;
    }

    /// <summary>
    ///     This method copies values from another camera to this one.
    /// </summary>
    /// <param name="camera">The other camera.</param>
    public void CopyFrom(DollarStoreCamera camera)
    {
        Viewport = camera.Viewport;
        InnerPosition = camera.Position;
        InnerVPMatrix = camera.GetVPMatrix();
        RenderScale = camera.RenderScale;
        Scale = camera.Scale;
    }

    protected override void SetMatrixValue(float left, float right, float bottom, float top)
    {
        left += _offset.X;
        right += _offset.X;

        bottom += _offset.Y;
        top += _offset.Y;
        base.SetMatrixValue(left, right, bottom, top);
    }

    public void Update(double secondsLastFrame)
    {
        if (Disposing) return;
        if (_pulseAnimation is { IsRunning: true })
        {
            var scaleAdd = _pulseAnimation.GetScale_Add(null!);
            Scale = RenderScale + scaleAdd.X;
        }
        else if (_pulseAnimation != null)
        {
            Scale = RenderScale;
            _pulseAnimation.Reset();
        }

        var current = Position;
        var target = _virtualPosition;

        Position = SteppingFunctions.Exponential(current, target, secondsLastFrame, 0.01f, _scrollSpeed);
    }

    public void Pulse(int times = 1, float frequency = 0)
    {
        _pulseAnimation ??= new PulseAnimation(times, frequency);
        _pulseAnimation.ResetWith(times, frequency);
    }

    public void SetOffset(Vector3 offset)
    {
        _offset = offset;
    }

    public Vector3 GetOffset()
    {
        return _offset;
    }

    public void ZoomStep(float scale)
    {
        const float stepping = .05f;
        var camera_scale = GetRenderScale();
        SetRenderScale(Math.Max(camera_scale + scale * stepping, stepping));
        OnZoom?.Invoke(RenderScale);
    }
}