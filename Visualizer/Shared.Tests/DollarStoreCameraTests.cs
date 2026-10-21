using OpenTK.Mathematics;

namespace Shared.Tests;

/// <summary>
///     The TDW-like follow mode asks whether a sound has left the view. Zoom scales about the
///     viewport's centre, so the view it asks about has to be the zoomed one, not the
///     camera's unzoomed rectangle.
/// </summary>
public class DollarStoreCameraTests
{
    private static readonly Vector3 Sound = (100, 0, 0);
    private static readonly Vector3 SoundSize = (64, 64, 0);

    [Fact]
    public void ZoomedInTheViewEndsShortOfTheViewport()
    {
        // 800 tall at 200%: the rows from 200 to 600 are on screen.
        var camera = new DollarStoreCamera(Vector3.Zero, (1000, 800));
        camera.SetRenderScale(2f);

        Assert.Equal(200f, camera.ZoomInset);
        Assert.False(camera.IsOutsideOfCameraView(Sound + (0, 300, 0), SoundSize));
        Assert.True(camera.IsOutsideOfCameraView(Sound + (0, 560, 0), SoundSize));
        Assert.True(camera.IsOutsideOfCameraView(Sound + (0, 150, 0), SoundSize));
    }

    [Fact]
    public void ZoomedOutTheViewReachesPastTheViewport()
    {
        // 800 tall at 50%: the rows from -400 to 1200 are on screen.
        var camera = new DollarStoreCamera(Vector3.Zero, (1000, 800));
        camera.SetRenderScale(0.5f);

        Assert.Equal(-400f, camera.ZoomInset);
        Assert.False(camera.IsOutsideOfCameraView(Sound + (0, 1000, 0), SoundSize));
        Assert.True(camera.IsOutsideOfCameraView(Sound + (0, 1150, 0), SoundSize));
    }

    [Fact]
    public void TheMarginIsKeptFromTheZoomedEdges()
    {
        var camera = new DollarStoreCamera(Vector3.Zero, (1000, 800));
        camera.SetRenderScale(2f);

        Assert.False(camera.IsOutsideOfCameraView(Sound + (0, 250, 0), SoundSize, 50));
        Assert.True(camera.IsOutsideOfCameraView(Sound + (0, 249, 0), SoundSize, 50));
        Assert.True(camera.IsOutsideOfCameraView(Sound + (0, 600 - 50 - 64 + 1, 0), SoundSize, 50));
    }
}
