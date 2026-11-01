using OpenTK.Mathematics;
using Shared.Animations;
using Sundex.Engine.Renderer.Cameras;

namespace Shared;

public sealed class DollarStoreCamera : Camera
{
    public Action<float>? OnZoom = null;
    private Vector3 _offset = (0, 0, 0);
    private PulseAnimation? _pulseAnimation;
    private Vector3 _virtualPosition;

    public DollarStoreCamera(Vector3 virtualPosition, Vector2i viewport, float scrollSpeed = 7.5f) : base(
        virtualPosition, viewport)
    {
        _virtualPosition = virtualPosition;
        PreciseY = virtualPosition.Y;
        ScrollSpeed = scrollSpeed;
        UpdateMatrix();
    }

    /// <summary>How fast the camera closes on where it is scrolling to. Settable: it comes
    /// from a setting, and that can move while the camera is alive.</summary>
    public float ScrollSpeed { get; set; }

    /// <summary>Where the camera is scrolling to, as opposed to where it has eased to so far.</summary>
    public Vector3 TargetPosition => _virtualPosition;

    /// <summary>
    ///     <see cref="Camera.Position" />'s Y in double. Tens of millions of pixels down a long
    ///     sequence a float's step is several pixels, so easing in float stutters; anything
    ///     drawn that far down draws relative to this instead (see the playfield).
    /// </summary>
    public double PreciseY { get; private set; }

    /// <summary>
    ///     How far below <see cref="Camera.Position" /> the visible top edge sits, and above the
    ///     bottom edge the visible bottom. Zoom scales about the viewport's centre, so 200%
    ///     shows a quarter of the height less at each edge, and 50% half a height more
    ///     (a negative inset).
    /// </summary>
    public float ZoomInset => (Height - Height / RenderScale) / 2f;

    public bool IsOutsideOfCameraView(Vector3 position, Vector3 scale, float marginFromTopBottom = 0)
    {
        var inset = ZoomInset;
        var collide_top = position.Y < _virtualPosition.Y + inset + marginFromTopBottom;
        var collide_bottom = position.Y + scale.Y > _virtualPosition.Y + Height - inset - marginFromTopBottom;

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
        PreciseY = position.Y;
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
        PreciseY = camera.PreciseY;
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

        // Position set from outside since the last step: take it over.
        if ((float)PreciseY != Position.Y) PreciseY = Position.Y;

        // SteppingFunctions.Exponential's smoothing, in double - see PreciseY.
        var current = new Vector3d(Position.X, PreciseY, Position.Z);
        var target = new Vector3d(_virtualPosition.X, _virtualPosition.Y, _virtualPosition.Z);
        current = Vector3d.Distance(current, target) < 0.01
            ? target
            : current + (target - current) * (1 - Math.Exp(-ScrollSpeed * secondsLastFrame));

        PreciseY = current.Y;
        Position = ((float)current.X, (float)current.Y, (float)current.Z);
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

    public void ZoomStep(float scale)
    {
        const float stepping = .05f;
        var camera_scale = GetRenderScale();
        SetRenderScale(Math.Max(camera_scale + scale * stepping, stepping));
        OnZoom?.Invoke(RenderScale);
    }
}