using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Animated;
using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class FramedAtlas(int width, int height) : ImageAtlas(width, height)
{
    protected Dictionary<int, QuadUV> FrameCoordinates { get; set; } = new();
    public QuadUV CurrentUV => FrameCoordinates[CurrentFrameIndex];
    
    public int CurrentFrameIndex { get; protected set; } = 0;
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

    public override void Update()
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

    public void Start() => TimingStopwatch.Start();

    public static FramedAtlas FromAnimatedTexture(string textureID, AssetTexture texture)
    {
        var animatedTexture = texture.As<AnimatedTexture>();
        var image = animatedTexture.GetData();
        if (image == null)
            throw new Exception("Failed to get image data from animated texture.");

        var frameCount = image.Frames.Count;
        if (frameCount <= 1)
            throw new Exception("Animated texture has less than 2 frames.");

        Vector2 imageSize = new(image.Width, image.Height);
        var atlasSize = GetAtlasSizeForTotalFrames(frameCount, imageSize);

        var atlas = new FramedAtlas(atlasSize.X, atlasSize.Y);
        for (var index = 0; index < image.Frames.Count; index++)
        {
            var frame = image.Frames[index];
            var coordinates = atlas.AddImage($"{textureID}-frame-{index}", frame);
            atlas.FrameCoordinates.Add(index, coordinates.TextureUV);
        }

        return atlas;
    }
}