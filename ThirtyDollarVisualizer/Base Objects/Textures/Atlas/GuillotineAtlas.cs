using SixLabors.ImageSharp;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

public sealed class GuillotineAtlas : IAtlas
{
    private readonly List<Rectangle> _freeRects = [];
    private readonly Dictionary<ImageFrame, Rectangle> _usedByImage;
    private readonly bool _allowRotation;

    private int _usedArea;

    public int Width { get; }
    public int Height { get; }

    public GuillotineAtlas(int width, int height, bool allowRotation = false)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Atlas dimensions must be positive.");

        Width = width;
        Height = height;
        _allowRotation = allowRotation;

        // Keep identity-based lookup for images.
        _usedByImage = new Dictionary<ImageFrame, Rectangle>(ReferenceEqualityComparer.Instance);

        _freeRects.Add(new Rectangle(0, 0, width, height));
        _usedArea = 0;
    }

    public bool CanFit(int width, int height)
    {
        if (width <= 0 || height <= 0) return false;
        return _freeRects.Any(r => Fits(r, width, height) || (_allowRotation && Fits(r, height, width)));
    }

    public bool IsFull()
    {
        return GetRemainingArea() == 0;
    }

    public int GetRemainingArea()
    {
        var total = _freeRects.Sum(r => (long)r.Width * r.Height);

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

    public Rectangle AddImage(ImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0) return Rectangle.Empty;

        var (index, placedW, placedH, rotate) = ChoosePlacement(image.Width, image.Height);
        if (index < 0) return Rectangle.Empty;

        var host = _freeRects[index];
        var placed = new Rectangle(host.X, host.Y, placedW, placedH);

        // Perform a guillotine split on the host rectangle.
        SplitFreeRect(index, placed, host);

        // Track usage.
        _usedByImage[image] = placed;
        _usedArea += placed.Width * placed.Height;

        return placed;
    }

    public bool RemoveImage(ImageFrame image)
    {
        if (!_usedByImage.Remove(image, out var rect)) return false;

        _usedArea -= rect.Width * rect.Height;
        AddFreeRect(rect);
        MergeAndPruneFreeRects();

        return true;
    }

    // Placement

    private (int index, int placedW, int placedH, bool rotated) ChoosePlacement(int w, int h)
    {
        var bestIndex = -1;
        var bestRotated = false;
        int bestW = 0, bestH = 0;
        var bestScore = new PlacementScore(int.MaxValue, int.MaxValue);

        for (var i = 0; i < _freeRects.Count; i++)
        {
            var r = _freeRects[i];

            // Try normal orientation
            PlacementScore score;
            
            if (Fits(r, w, h))
            {
                score = ScorePlacement(r, w, h);
                if (IsBetter(score, bestScore))
                {
                    bestScore = score;
                    bestIndex = i;
                    bestRotated = false;
                    bestW = w;
                    bestH = h;
                }
            }

            // Try rotated if allowed
            if (!_allowRotation || !Fits(r, h, w)) continue;
            score = ScorePlacement(r, h, w);
            if (!IsBetter(score, bestScore)) continue;
            bestScore = score;
            bestIndex = i;
            bestRotated = true;
            bestW = h;
            bestH = w;
        }

        return (bestIndex, bestW, bestH, bestRotated);
    }

    private static PlacementScore ScorePlacement(Rectangle host, int w, int h)
    {
        var areaWaste = host.Width * host.Height - w * h;
        var maxSideLeftover = Math.Max(host.Width - w, host.Height - h);
        return new PlacementScore(areaWaste, maxSideLeftover);
    }

    private static bool IsBetter(PlacementScore a, PlacementScore b)
    {
        if (a.AreaWaste != b.AreaWaste) return a.AreaWaste < b.AreaWaste;
        return a.MaxSideLeftover < b.MaxSideLeftover;
    }

    private static bool Fits(Rectangle r, int w, int h)
    {
        return w <= r.Width && h <= r.Height;
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

    private void AddFreeRect(in Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        _freeRects.Add(r);
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
        }
        while (merged);

        // Final prune in case merges created containment
        PruneContained();
    }

    private void PruneContained()
    {
        // Remove any rectangle fully contained in another
        for (var i = 0; i < _freeRects.Count; i++)
        {
            var a = _freeRects[i];
            for (var j = _freeRects.Count - 1; j >= 0; j--)
            {
                if (i == j) continue;
                var b = _freeRects[j];
                if (Contains(a, b))
                {
                    _freeRects.RemoveAt(j);
                    if (j < i) i--; // adjust i if needed
                }
                else if (Contains(b, a))
                {
                    _freeRects.RemoveAt(i);
                    i--;
                    break;
                }
            }
        }
    }

    private bool TryMergeOnce()
    {
        for (var i = 0; i < _freeRects.Count; i++)
        {
            for (var j = i + 1; j < _freeRects.Count; j++)
            {
                var a = _freeRects[i];
                var b = _freeRects[j];

                // Horizontal merge (same Y and Height, touching in X)
                Rectangle merged;
                if (a.Y == b.Y && a.Height == b.Height)
                {
                    if (a.X + a.Width == b.X)
                    {
                        merged = new Rectangle(a.X, a.Y, a.Width + b.Width, a.Height);
                        _freeRects[i] = merged;
                        _freeRects.RemoveAt(j);
                        return true;
                    }
                    if (b.X + b.Width == a.X)
                    {
                        merged = new Rectangle(b.X, b.Y, b.Width + a.Width, b.Height);
                        _freeRects[i] = merged;
                        _freeRects.RemoveAt(j);
                        return true;
                    }
                }

                // Vertical merge (same X and Width, touching in Y)
                if (a.X != b.X || a.Width != b.Width) continue;
                if (a.Y + a.Height == b.Y)
                {
                    merged = new Rectangle(a.X, a.Y, a.Width, a.Height + b.Height);
                    _freeRects[i] = merged;
                    _freeRects.RemoveAt(j);
                    return true;
                }

                if (b.Y + b.Height != a.Y) continue;
                merged = new Rectangle(b.X, b.Y, b.Width, b.Height + a.Height);
                _freeRects[i] = merged;
                _freeRects.RemoveAt(j);
                return true;
            }
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