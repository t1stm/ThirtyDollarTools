using SixLabors.ImageSharp;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

public interface IAtlas
{
    int Width { get; }
    int Height { get; }
    bool CanFit(int width, int height);
    bool IsFull();
    int GetRemainingArea();
    int GetUsedArea();
    float GetUsagePercentage();
    Rectangle AddImage(ImageFrame image);
    bool RemoveImage(ImageFrame image);
}