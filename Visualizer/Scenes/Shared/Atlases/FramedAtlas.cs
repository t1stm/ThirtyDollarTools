using System.Diagnostics;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Texture;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;
using ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;

namespace Shared.Atlases;

public class FramedAtlas(int width, int height) : GPUTextureAtlas(width, height)
{
    public Rectangle CurrentRectangle => FrameCoordinates[CurrentFrameIndex];

    private Dictionary<int, Rectangle> FrameCoordinates { get; } = new();
    private Dictionary<int, float> FrameDurationMap { get; } = new();
    private float CurrentFrameStartTime { get; set; }
    private int CurrentFrameIndex { get; set; }
    private float TotalLength { get; set; }
    private int FrameCount => FrameCoordinates.Count;

    protected Stopwatch TimingStopwatch { get; set; } = new();

    private static Vector2i GetAtlasSizeForTotalFrames(int frameCount, Vector2 imageSize)
    {
        // Add padding to the frame size to match GuillotineAtlas logic
        const int padding = 2;
        var paddedWidth = (int)imageSize.X + padding * 2;
        var paddedHeight = (int)imageSize.Y + padding * 2;

        var aspectRatio = (float)paddedWidth / paddedHeight;

        // Start by guessing columns based on the frame count and aspect ratio to keep the atlas somewhat square
        var columns = (int)Math.Ceiling(Math.Sqrt(frameCount * aspectRatio));
        if (columns < 1) columns = 1;

        var rows = (int)Math.Ceiling((double)frameCount / columns);

        var optimalWidth = columns * paddedWidth;
        var optimalHeight = rows * paddedHeight;

        return new Vector2i(optimalWidth, optimalHeight);
    }

    public void Update()
    {
        if (!TimingStopwatch.IsRunning) return;
        var elapsed = TimingStopwatch.ElapsedMilliseconds % TotalLength;

        if (elapsed < CurrentFrameStartTime)
        {
            CurrentFrameStartTime = 0;
            CurrentFrameIndex = 0;
        }

        var currentLength = CurrentFrameStartTime;
        for (var i = CurrentFrameIndex; i < FrameCount; i++)
        {
            var nextLength = currentLength + FrameDurationMap[i];
            if (elapsed < nextLength)
            {
                CurrentFrameIndex = i;
                CurrentFrameStartTime = currentLength;
                break;
            }

            currentLength = nextLength;
        }
    }

    public void Start()
    {
        if (TimingStopwatch.IsRunning) return;
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

        float length = 0;
        for (var index = 0; index < image.Frames.Count; index++)
        {
            var frame = image.Frames[index];
            var textureName = $"{textureID}-frame-{index}";
            atlas.AddTexture(textureName, frame);

            var rect = atlas.Atlas.GetImageRectangle(textureName);
            if (rect.IsEmpty)
                throw new Exception("Failed to get image data from texture.");

            atlas.FrameCoordinates.Add(index, rect);
            length += atlas.FrameDurationMap[index] = TryGetFrameDelay(frame) ?? 100f;
        }

        atlas.TotalLength = length;
        return atlas;
    }

    public void SetUniforms(Shader shader)
    {
        var quad = QuadUV.FromRectangle(CurrentRectangle, Width, Height);
        shader.SetUniform("u_UV", quad.UV);
    }

    private static float? TryGetFrameDelay(ImageFrame frame)
    {
        if (frame.Metadata.TryGetGifMetadata(out var gif))
            return gif.FrameDelay * 10f;

        if (frame.Metadata.TryGetPngMetadata(out var png))
            return png.FrameDelay.ToSingle() * 100f;

        if (frame.Metadata.TryGetWebpFrameMetadata(out var webp))
            return webp.FrameDelay;

        return null;
    }
}