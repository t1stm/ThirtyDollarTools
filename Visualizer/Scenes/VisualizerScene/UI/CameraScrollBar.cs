using System.Diagnostics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Engine.Renderer.Cameras;

namespace VisualizerScene.UI;

/// <summary>
///     The page scroll bar down the right edge: drawn the way Chrome draws thirtydollar.website's
///     (the <c>::-webkit-scrollbar</c> rules in its stylesheet) and driven the way Chrome drives
///     one - drag the thumb, or press the track to page towards the pointer, repeating while
///     held. Hidden when the page fits, as <c>overflow: auto</c> hides it. It follows the
///     camera's position, which zoom doesn't move (zoom scales about the viewport's centre), and
///     is drawn in screen space, so zoom leaves it alone. Fields and value types only: a frame
///     of it allocates nothing.
/// </summary>
public sealed class CameraScrollBar(PlayfieldContainer playfield)
{
    private const float Thickness = 12f;

    /// <summary>The stylesheet asks for 10, which a browser clamps to half the box. The shader doesn't.</summary>
    private const float Radius = Thickness / 2f;

    private const float MinThumbLength = 24f;

    /// <summary>
    ///     Where the scene parks the camera on load, so the top of the page. The page keeps the
    ///     same margin past the playfield's end.
    /// </summary>
    private const float Top = -300f;

    // Chrome's track paging: 87.5% of the view a step, again 250 ms into a hold and every 50 ms after.
    private const float PageFraction = 0.875f;
    private const long RepeatDelayMs = 250;
    private const long RepeatIntervalMs = 50;

    private readonly Stopwatch _holdStopwatch = new();
    private readonly ColoredPlane _thumb = new() { Color = (0, 0, 0, 0.33f), BorderRadius = Radius };
    private readonly ColoredPlane _track = new() { Color = (0, 0, 0, 0.15f), BorderRadius = Radius };

    /// <summary>The lowest the page scrolls to.</summary>
    private float _bottom;

    private bool _dragging;
    private float _grabOffset;
    private float _left;
    private long _nextRepeatMs;

    /// <summary>-1 or +1 while the track is held, 0 otherwise.</summary>
    private int _pageDirection;

    private float _thumbLength;
    private float _viewHeight;

    private bool Visible => _bottom > Top;

    /// <returns>
    ///     Whether the bar has the pointer: pressed on it and held. The player bar under the
    ///     track's foot mustn't act on that press as well.
    /// </returns>
    public bool Mouse(MouseState mouse)
    {
        Measure();
        if (!mouse.IsButtonDown(MouseButton.Left) || !Visible)
        {
            _dragging = false;
            _pageDirection = 0;
            return false;
        }

        var camera = playfield.Camera;
        var y = mouse.Position.Y;
        if (mouse.IsButtonPressed(MouseButton.Left))
        {
            if (mouse.Position.X < _left) return false;

            var thumbTop = ThumbTop(camera.Position.Y);
            if (y >= thumbTop && y < thumbTop + _thumbLength)
            {
                _dragging = true;
                _grabOffset = y - thumbTop;
            }
            else
            {
                _pageDirection = y < thumbTop ? -1 : 1;
                _nextRepeatMs = 0;
                _holdStopwatch.Restart();
            }
        }

        if (_dragging)
        {
            // Straight there, no easing: the page sticks to the pointer.
            var fraction = Math.Clamp((y - _grabOffset) / (_viewHeight - _thumbLength), 0, 1);
            var position = camera.Position;
            position.Y = Top + fraction * (_bottom - Top);
            camera.SetPosition(position);
            return true;
        }

        if (_pageDirection == 0) return false;
        if (_holdStopwatch.ElapsedMilliseconds < _nextRepeatMs) return true;
        _nextRepeatMs += _nextRepeatMs == 0 ? RepeatDelayMs : RepeatIntervalMs;

        // Paging stops once the thumb reaches the pointer. Judged by where the camera is headed,
        // or a held press would keep paging while the last step is still easing in.
        var target = camera.TargetPosition;
        var top = ThumbTop(target.Y);
        if (_pageDirection < 0 ? y >= top : y < top + _thumbLength) return true;

        target.Y = Math.Clamp(target.Y + _pageDirection * PageFraction * _viewHeight, Top, _bottom);
        camera.ScrollTo(target);
        return true;
    }

    /// <param name="screenCamera">An unzoomed camera over the window, in pixels.</param>
    public void Render(Camera screenCamera)
    {
        Measure();
        if (!Visible) return;

        _track.Position = (_left, 0, 0);
        _track.Scale = (Thickness, _viewHeight, 0);
        _thumb.Position = (_left, ThumbTop(playfield.Camera.Position.Y), 0);
        _thumb.Scale = (Thickness, _thumbLength, 0);

        _track.Render(screenCamera);
        _thumb.Render(screenCamera);
    }

    private void Measure()
    {
        var camera = playfield.Camera;
        _viewHeight = camera.Height;
        _left = camera.Width - Thickness;
        _bottom = playfield.ContentHeight - Top - _viewHeight;

        var pageHeight = _bottom - Top + _viewHeight;
        _thumbLength = Math.Min(_viewHeight, Math.Max(MinThumbLength, _viewHeight * _viewHeight / pageHeight));
    }

    /// <summary>
    ///     The thumb's top for a camera at <paramref name="cameraY" />. Clamped: wheel scrolling
    ///     and the follow modes take the camera past either end of the page.
    /// </summary>
    private float ThumbTop(float cameraY)
    {
        return Math.Clamp((cameraY - Top) / (_bottom - Top), 0, 1) * (_viewHeight - _thumbLength);
    }
}
