using System.Diagnostics;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Texture;
using ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class FramedAtlas(int width, int height) : GPUTextureAtlas(width, height)
{
    protected Dictionary<int, Rectangle> FrameCoordinates { get; set; } = new();
    public Rectangle CurrentRectangle => FrameCoordinates[CurrentFrameIndex];

    public int CurrentFrameIndex { get; protected set; }
    public int FrameCount => FrameCoordinates.Count;
    public float FrameDurationMilliseconds { get; protected set; } = 1f;
    protected Stopwatch TimingStopwatch { get; set; } = new();

    private static Vector2i GetAtlasSizeForTotalFrames(int frameCount, Vector2 imageSize)
    {
        // Calculate optimal atlas size for best fit
        var totalArea = frameCount * imageSize.X * imageSize.Y;
        var aspectRatio = imageSize.X / imageSize.Y;

        // Start with square root of total area and adjust based on aspect ratio
        var baseSize = (int)Math.Ceiling(Math.Sqrt(totalArea));

        // Calculate optimal dimensions considering frame aspect ratio
        var optimalWidth = (int)Math.Ceiling(baseSize * Math.Sqrt(aspectRatio));
        var optimalHeight = (int)Math.Ceiling(baseSize / Math.Sqrt(aspectRatio));

        // Ensure dimensions can fit at least one frame
        optimalWidth = Math.Max(optimalWidth, (int)imageSize.X);
        optimalHeight = Math.Max(optimalHeight, (int)imageSize.Y);

        // Add some padding (10% extra space) to account for packing inefficiency
        optimalWidth = (int)(optimalWidth * 1.1f);
        optimalHeight = (int)(optimalHeight * 1.1f);

        Vector2i atlasSize = new(optimalWidth, optimalHeight);

        return atlasSize;
    }

    public void Update()
    {
        var elapsed = TimingStopwatch.ElapsedMilliseconds;
        var currentFrame = elapsed / FrameDurationMilliseconds;
        var currentFrameFloored = (int)MathF.Floor(currentFrame);

        if (currentFrameFloored == CurrentFrameIndex)
            return;

        if (currentFrameFloored >= FrameCount)
            TimingStopwatch.Reset();

        CurrentFrameIndex = currentFrameFloored % FrameCount;
    }

    public void Start()
    {
        TimingStopwatch.Start();
    }

    public static FramedAtlas FromAnimatedTexture(string textureID, TextureHolder texture)
    {
        var image = texture.Texture;
        var frameCount = image.Frames.Count;

        if (frameCount <= 1)
            throw new Exception("Animated texture has less than 2 frames.");

        Vector2 imageSize = new(image.Width, image.Height);
        var atlasSize = GetAtlasSizeForTotalFrames(frameCount, imageSize);

        var atlas = new FramedAtlas(atlasSize.X, atlasSize.Y)
        {
            AtlasID = "FramedAtlas_" + textureID
        };
        for (var index = 0; index < image.Frames.Count; index++)
        {
            var frame = image.Frames[index];
            var textureName = $"{textureID}-frame-{index}";
            atlas.AddTexture(textureName, frame);

            var rect = atlas.Atlas.GetImageRectangle(textureName);
            atlas.FrameCoordinates.Add(index, rect);
        }

        return atlas;
    }
}