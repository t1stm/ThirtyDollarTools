using SixLabors.ImageSharp;

namespace ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;

public interface IAtlas
{
    int Width { get; }
    int Height { get; }
    bool CanFit(int width, int height);
    bool IsFull();
    int GetRemainingArea();
    int GetUsedArea();
    float GetUsagePercentage();
    Rectangle AddImage(string imageID, ImageFrame image);
    bool RemoveImage(string imageID);
}