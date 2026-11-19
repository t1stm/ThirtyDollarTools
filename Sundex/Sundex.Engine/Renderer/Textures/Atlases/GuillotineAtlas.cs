using System.Text.Json.Serialization;
using SixLabors.ImageSharp;

namespace Sundex.Engine.Renderer.Textures.Atlases;

public sealed class GuillotineAtlas : IAtlas
{
    [JsonInclude] [JsonPropertyName("rotatable")]
    private readonly bool _allowRotation;

    [JsonInclude] [JsonPropertyName("freeRects")]
    private readonly List<Rectangle> _freeRects = [];

    [JsonInclude] [JsonPropertyName("imageCoordsDictionary")]
    private readonly Dictionary<string, Rectangle> _usedByImageID;

    [JsonInclude] [JsonPropertyName("usedArea")]
    private int _usedArea;

    public GuillotineAtlas(int width, int height, bool allowRotation = false)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Atlas dimensions must be positive.");

        Width = width;
        Height = height;
        _allowRotation = allowRotation;

        _usedByImageID = new Dictionary<string, Rectangle>();
        _freeRects.Add(new Rectangle(0, 0, width, height));
        _usedArea = 0;
    }

    [JsonInclude] public int Padding { get; init; } = 2;

    [JsonInclude] public int Width { get; }

    [JsonInclude] public int Height { get; }

    public bool CanFit(int width, int height)
    {
        if (width <= 0 || height <= 0) return false;
        var paddedW = width + Padding * 2;
        var paddedH = height + Padding * 2;
        return _freeRects.Any(free => Fits(free, paddedW, paddedH) || (_allowRotation && Fits(free, paddedH, paddedW)));
    }

    public bool IsFull()
    {
        return GetRemainingArea() == 0;
    }

    public int GetRemainingArea()
    {
        var total = _freeRects.Sum(free => (long)free.Width * free.Height);

        // Cap to int bounds (atlas sizes are expected to be reasonable).
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    public int GetUsedArea()
    {
        return _usedArea;
    }

    public float GetUsagePercentage()
    {
        var total = (long)Width * Height;
        return total == 0 ? 0f : Math.Clamp((float)_usedArea / total, 0f, 1f);
    }

    public Rectangle AddImage(string imageID, ImageFrame image)
    {
        return AddImage(imageID.AsSpan(), image);
    }

    public bool RemoveImage(string imageID)
    {
        return RemoveImage(imageID.AsSpan());
    }

    public Rectangle GetImageRectangle(string imageID)
    {
        return GetImageRectangle(imageID.AsSpan());
    }

    public Rectangle GetImageRectangle(ReadOnlySpan<char> imageID)
    {
        lock (_usedByImageID)
        {
            var alternativeLookup = _usedByImageID.GetAlternateLookup<ReadOnlySpan<char>>();
            return alternativeLookup.TryGetValue(imageID, out var rect) ? rect : Rectangle.Empty;
        }
    }

    public Rectangle AddImage(ReadOnlySpan<char> imageID, ImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_usedByImageID)
        {
            var alternativeLookup = _usedByImageID.GetAlternateLookup<ReadOnlySpan<char>>();
            if (image.Width <= 0 || image.Height <= 0) return Rectangle.Empty;

            var paddedW = image.Width + Padding * 2;
            var paddedH = image.Height + Padding * 2;
            var (index, placedW, placedH, rotate) = ChoosePlacement(paddedW, paddedH);
            if (index < 0) return Rectangle.Empty;

            var host = _freeRects[index];
            var placed = new Rectangle(host.X, host.Y, placedW, placedH);

            // Perform a guillotine split on the host rectangle.
            SplitFreeRect(index, placed, host);

            // Track usage.
            var rect = new Rectangle(placed.X + Padding, placed.Y + Padding, image.Width, image.Height);
            alternativeLookup[imageID] = rect;
            _usedArea += placed.Width * placed.Height;

            return rect;
        }
    }

    public bool RemoveImage(ReadOnlySpan<char> imageID)
    {
        lock (_usedByImageID)
        {
            var alternativeLookup = _usedByImageID.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alternativeLookup.Remove(imageID, out _, out var rect)) return false;

            _usedArea -= rect.Width * rect.Height;
            AddFreeRect(rect);
            MergeAndPruneFreeRects();

            return true;
        }
    }

    // Placement

    private (int index, int placedW, int placedH, bool rotated) ChoosePlacement(int width, int height)
    {
        var bestIndex = -1;
        var bestRotated = false;
        int bestW = 0, bestH = 0;
        var bestScore = new PlacementScore(int.MaxValue, int.MaxValue);

        for (var index = 0; index < _freeRects.Count; index++)
        {
            var rectangle = _freeRects[index];

            // Try normal orientation
            PlacementScore score;

            if (Fits(rectangle, width, height))
            {
                score = ScorePlacement(rectangle, width, height);
                if (IsBetter(score, bestScore))
                {
                    bestScore = score;
                    bestIndex = index;
                    bestRotated = false;
                    bestW = width;
                    bestH = height;
                }
            }

            // Try rotated if allowed
            if (!_allowRotation || !Fits(rectangle, height, width)) continue;
            score = ScorePlacement(rectangle, height, width);
            if (!IsBetter(score, bestScore)) continue;
            bestScore = score;
            bestIndex = index;
            bestRotated = true;
            bestW = height;
            bestH = width;
        }

        return (bestIndex, bestW, bestH, bestRotated);
    }

    private static PlacementScore ScorePlacement(Rectangle host, int width, int height)
    {
        var areaWaste = host.Width * host.Height - width * height;
        var maxSideLeftover = Math.Max(host.Width - width, host.Height - height);
        return new PlacementScore(areaWaste, maxSideLeftover);
    }

    private static bool IsBetter(PlacementScore first, PlacementScore second)
    {
        if (first.AreaWaste != second.AreaWaste) return first.AreaWaste < second.AreaWaste;
        return first.MaxSideLeftover < second.MaxSideLeftover;
    }

    private static bool Fits(Rectangle rectangle, int width, int height)
    {
        return width <= rectangle.Width && height <= rectangle.Height;
    }

    // Splitting and free-space maintenance

    private void SplitFreeRect(int hostIndex, Rectangle placed, Rectangle host)
    {
        // Remove the used host rect
        _freeRects.RemoveAt(hostIndex);

        // Choose split orientation: shorter leftover axis heuristic
        var leftoverHoriz = host.Width - placed.Width;
        var leftoverVert = host.Height - placed.Height;
        var splitVertical = leftoverHoriz <= leftoverVert;

        if (splitVertical)
        {
            // Vertical split:
            // Right rect takes full host height to the right of placed
            var right = new Rectangle(
                placed.X + placed.Width,
                host.Y,
                host.Width - placed.Width,
                host.Height);

            // Bottom rect takes the width of the placed, below it
            var bottom = new Rectangle(
                host.X,
                placed.Y + placed.Height,
                placed.Width,
                host.Height - placed.Height);

            AddFreeRect(right);
            AddFreeRect(bottom);
        }
        else
        {
            // Horizontal split:
            // Bottom rect takes full host width below the placed
            var bottom = new Rectangle(
                host.X,
                placed.Y + placed.Height,
                host.Width,
                host.Height - placed.Height);

            // Right rect takes the height of the placed, to the right of it
            var right = new Rectangle(
                placed.X + placed.Width,
                host.Y,
                host.Width - placed.Width,
                placed.Height);

            AddFreeRect(right);
            AddFreeRect(bottom);
        }

        MergeAndPruneFreeRects();
    }

    private void AddFreeRect(in Rectangle rectangle)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0) return;
        _freeRects.Add(rectangle);
    }

    private void MergeAndPruneFreeRects()
    {
        // Remove contained rectangles to keep list minimal
        PruneContained();

        // Merge neighbors that share edges and dimensions along the opposing axis
        bool merged;
        do
        {
            merged = TryMergeOnce();
        } while (merged);

        // Final prune in case merges created containment
        PruneContained();
    }

    private void PruneContained()
    {
        // Remove any rectangle fully contained in another
        for (var index = 0; index < _freeRects.Count; index++)
        {
            var candidate = _freeRects[index];
            for (var otherIndex = _freeRects.Count - 1; otherIndex >= 0; otherIndex--)
            {
                if (index == otherIndex) continue;
                var other = _freeRects[otherIndex];
                if (Contains(candidate, other))
                {
                    _freeRects.RemoveAt(otherIndex);
                    if (otherIndex < index) index--; // adjust i if needed
                }
                else if (Contains(other, candidate))
                {
                    _freeRects.RemoveAt(index);
                    index--;
                    break;
                }
            }
        }
    }

    private bool TryMergeOnce()
    {
        for (var index = 0; index < _freeRects.Count; index++)
        for (var otherIndex = index + 1; otherIndex < _freeRects.Count; otherIndex++)
        {
            var candidate = _freeRects[index];
            var other = _freeRects[otherIndex];

            // Horizontal merge (same Y and Height, touching in X)
            Rectangle merged;
            if (candidate.Y == other.Y && candidate.Height == other.Height)
            {
                if (candidate.X + candidate.Width == other.X)
                {
                    merged = new Rectangle(candidate.X, candidate.Y, candidate.Width + other.Width, candidate.Height);
                    _freeRects[index] = merged;
                    _freeRects.RemoveAt(otherIndex);
                    return true;
                }

                if (other.X + other.Width == candidate.X)
                {
                    merged = new Rectangle(other.X, other.Y, other.Width + candidate.Width, other.Height);
                    _freeRects[index] = merged;
                    _freeRects.RemoveAt(otherIndex);
                    return true;
                }
            }

            // Vertical merge (same X and Width, touching in Y)
            if (candidate.X != other.X || candidate.Width != other.Width) continue;
            if (candidate.Y + candidate.Height == other.Y)
            {
                merged = new Rectangle(candidate.X, candidate.Y, candidate.Width, candidate.Height + other.Height);
                _freeRects[index] = merged;
                _freeRects.RemoveAt(otherIndex);
                return true;
            }

            if (other.Y + other.Height != candidate.Y) continue;
            merged = new Rectangle(other.X, other.Y, other.Width, other.Height + candidate.Height);
            _freeRects[index] = merged;
            _freeRects.RemoveAt(otherIndex);
            return true;
        }

        return false;
    }

    // Geometry helpers

    private static bool Contains(in Rectangle outer, in Rectangle inner)
    {
        return outer.X <= inner.X
               && outer.Y <= inner.Y
               && outer.X + outer.Width >= inner.X + inner.Width
               && outer.Y + outer.Height >= inner.Y + inner.Height;
    }

    // Scoring struct

    private readonly record struct PlacementScore(int AreaWaste, int MaxSideLeftover);
}