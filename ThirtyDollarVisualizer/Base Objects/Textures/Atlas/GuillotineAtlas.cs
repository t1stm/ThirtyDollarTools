using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

/// <summary>
/// Implements a texture atlas using the Guillotine bin packing algorithm.
/// This class efficiently packs multiple images into a single texture atlas,
/// optimizing space usage through rectangle splitting techniques.
/// </summary>
/// <param name="width">The width of the atlas in pixels</param>
/// <param name="height">The height of the atlas in pixels</param>
public class GuillotineAtlas(int width, int height)
{
    /// <summary>
    /// Gets the width of the atlas in pixels.
    /// </summary>
    public int Width { get; } = width;
    
    /// <summary>
    /// Gets the height of the atlas in pixels.
    /// </summary>
    public int Height { get; } = height;

    private readonly List<Rectangle> _images = [];
    private readonly List<Rectangle> _freeRectangles = [];
    private bool _isInitialized;

    // Cache for performance - avoids repeated allocations
    private readonly List<int> _sortedIndicesCache = [];

    /// <summary>
    /// Initializes a new instance of the GuillotineAtlas class with default dimensions (1024x1024).
    /// </summary>
    public GuillotineAtlas() : this(1024, 1024) { }

    /// <summary>
    /// Creates a new atlas with the specified images already packed.
    /// </summary>
    /// <param name="images">Array of images to pack into the atlas</param>
    /// <param name="width">The width of the atlas in pixels (default: 1024)</param>
    /// <param name="height">The height of the atlas in pixels (default: 1024)</param>
    /// <returns>A new GuillotineAtlas instance with all images packed</returns>
    /// <exception cref="InvalidOperationException">Thrown when images cannot fit in the specified atlas dimensions</exception>
    public static GuillotineAtlas WithImages(Image<Rgba32>[] images, int width = 1024, int height = 1024)
    {
        var atlas = new GuillotineAtlas(width, height);
        
        foreach (var image in images)
            atlas.AddImage(image);
        
        atlas.ComputeAtlas();
        return atlas;
    }
    
    /// <summary>
    /// Computes the optimal layout for all added images using the Guillotine bin packing algorithm.
    /// Images are sorted by area (largest first) for better space utilization.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not all images can fit in the atlas</exception>
    public void ComputeAtlas()
    {
        if (_images.Count == 0)
        {
            _isInitialized = true;
            _freeRectangles.Clear();
            _freeRectangles.Add(new Rectangle(0, 0, Width, Height));
            return;
        }

        // Reuse cached list to avoid allocations
        _sortedIndicesCache.Clear();
        _sortedIndicesCache.EnsureCapacity(_images.Count);
        
        for (var i = 0; i < _images.Count; i++)
        {
            _sortedIndicesCache.Add(i);
        }

        // Sort by area (largest first) for better packing
        _sortedIndicesCache.Sort((i, j) => 
            (_images[j].Width * _images[j].Height).CompareTo(_images[i].Width * _images[i].Height));

        // Reset all image positions to unplaced
        for (var i = 0; i < _images.Count; i++)
        {
            var image = _images[i];
            _images[i] = new Rectangle(-1, -1, image.Width, image.Height);
        }

        var packed = TryPackImages(_sortedIndicesCache);

        if (!packed)
        {
            throw new InvalidOperationException($"Failed to pack all images into atlas of size {Width}x{Height}. Atlas is too small for the provided images.");
        }

        _isInitialized = true;
    }

    private bool TryPackImages(List<int> sortedIndices)
    {
        // Reset free rectangles
        _freeRectangles.Clear();
        _freeRectangles.Add(new Rectangle(0, 0, Width, Height));

        // Reset image locations
        for (var i = 0; i < _images.Count; i++)
        {
            var image = _images[i];
            _images[i] = new Rectangle(-1, -1, image.Width, image.Height);
        }

        // Try to pack each image
        foreach (var imageIndex in sortedIndices)
        {
            var imageRect = _images[imageIndex];
            var position = FindBestFit(imageRect.Width, imageRect.Height);

            if (position.X == -1 || position.Y == -1)
            {
                return false; // Failed to pack this image
            }

            _images[imageIndex] = new Rectangle(position.X, position.Y, imageRect.Width, imageRect.Height);
            
            // Split the used rectangle
            SplitRectangle(_images[imageIndex]);
        }

        return true;
    }

    private Vector2i FindBestFit(int width, int height)
    {
        var bestIndex = -1;
        var bestShortSide = int.MaxValue;
        var bestLongSide = int.MaxValue;

        // Use for loop instead of foreach for better performance
        for (var i = 0; i < _freeRectangles.Count; i++)
        {
            var rect = _freeRectangles[i];

            if (rect.Width < width || rect.Height < height) continue;
            
            var leftoverHorizontal = rect.Width - width;
            var leftoverVertical = rect.Height - height;
            var shortSide = Math.Min(leftoverHorizontal, leftoverVertical);
            var longSide = Math.Max(leftoverHorizontal, leftoverVertical);

            if (shortSide >= bestShortSide && (shortSide != bestShortSide || longSide >= bestLongSide)) continue;
            
            bestIndex = i;
            bestShortSide = shortSide;
            bestLongSide = longSide;
        }

        return bestIndex == -1 ? new Vector2i(-1, -1) : new Vector2i(_freeRectangles[bestIndex].X, _freeRectangles[bestIndex].Y);
    }

    private void SplitRectangle(Rectangle usedRect)
    {
        // Process in reverse order to avoid index shifting issues
        for (var i = _freeRectangles.Count - 1; i >= 0; i--)
        {
            var rect = _freeRectangles[i];
            if (!rect.IntersectsWith(usedRect)) continue;
            
            _freeRectangles.RemoveAt(i);
                
            // Add new rectangles efficiently - only if they have positive area
            if (usedRect.X > rect.X)
            {
                _freeRectangles.Add(new Rectangle(rect.X, rect.Y, usedRect.X - rect.X, rect.Height));
            }
                
            if (usedRect.Right < rect.Right)
            {
                _freeRectangles.Add(new Rectangle(usedRect.Right, rect.Y, rect.Right - usedRect.Right, rect.Height));
            }
                
            if (usedRect.Y > rect.Y)
            {
                _freeRectangles.Add(new Rectangle(rect.X, rect.Y, rect.Width, usedRect.Y - rect.Y));
            }
                
            if (usedRect.Bottom < rect.Bottom)
            {
                _freeRectangles.Add(new Rectangle(rect.X, usedRect.Bottom, rect.Width, rect.Bottom - usedRect.Bottom));
            }
        }

        // Remove any rectangles that are contained within other rectangles
        PruneFreeRectangles();
    }

    private void PruneFreeRectangles()
    {
        // More efficient pruning - mark for removal instead of removing during iteration
        var toRemove = new List<int>();
        
        for (var i = 0; i < _freeRectangles.Count; i++)
        {
            for (var j = i + 1; j < _freeRectangles.Count; j++)
            {
                if (IsContainedIn(_freeRectangles[i], _freeRectangles[j]))
                {
                    toRemove.Add(i);
                    break;
                }

                if (IsContainedIn(_freeRectangles[j], _freeRectangles[i]))
                {
                    toRemove.Add(j);
                }
            }
        }

        // Remove in reverse order to maintain indices
        for (var i = toRemove.Count - 1; i >= 0; i--)
        {
            _freeRectangles.RemoveAt(toRemove[i]);
        }
    }

    private static bool IsContainedIn(Rectangle rect1, Rectangle rect2)
    {
        return rect1.X >= rect2.X && rect1.Y >= rect2.Y &&
               rect1.Right <= rect2.Right && rect1.Bottom <= rect2.Bottom;
    }

    /// <summary>
    /// Determines whether the atlas is completely full, with no remaining space for new images.
    /// </summary>
    /// <returns>true if the atlas is full; otherwise, false</returns>
    public bool IsFull()
    {
        return _isInitialized && (_freeRectangles.Count == 0 || _freeRectangles.All(rect => rect.Width <= 0 || rect.Height <= 0));
    }

    /// <summary>
    /// Checks if an image with the specified dimensions can fit in the remaining space.
    /// </summary>
    /// <param name="width">The width of the image to check</param>
    /// <param name="height">The height of the image to check</param>
    /// <returns>true if the image can fit, otherwise false</returns>
    public bool CanFit(int width, int height)
    {
        if (!_isInitialized) return true;
        
        // Early exit optimization
        if (width <= 0 || height <= 0) return false;
        
        // Quick check against the largest available rectangle
        var maxAvailableWidth = _freeRectangles.Count > 0 ? _freeRectangles.Max(r => r.Width) : 0;
        var maxAvailableHeight = _freeRectangles.Count > 0 ? _freeRectangles.Max(r => r.Height) : 0;
        
        if (width > maxAvailableWidth || height > maxAvailableHeight) return false;

        return FindBestFit(width, height) != new Vector2i(-1, -1);
    }

    /// <summary>
    /// Checks if the specified image can fit in the remaining space.
    /// </summary>
    /// <param name="image">The image to check</param>
    /// <returns>true if the image can fit; otherwise, false</returns>
    public bool CanFit(Image image) => CanFit(image.Width, image.Height);

    /// <summary>
    /// Calculates the total remaining area in the atlas that can be used for new images.
    /// </summary>
    /// <returns>The remaining area in square pixels</returns>
    public int GetRemainingArea()
    {
        if (!_isInitialized) return Width * Height;
        
        // Use LINQ Sum for cleaner code, but consider caching if called frequently
        return _freeRectangles.Sum(rect => rect.Width * rect.Height);
    }

    /// <summary>
    /// Calculates the total area currently used by images in the atlas.
    /// </summary>
    /// <returns>The used area in square pixels</returns>
    public int GetUsedArea() => Width * Height - GetRemainingArea();

    /// <summary>
    /// Calculates the percentage of the atlas that is currently being used.
    /// </summary>
    /// <returns>The usage percentage as a float between 0 and 100</returns>
    public float GetUsagePercentage() => (float)GetUsedArea() / (Width * Height) * 100f;
    
    /// <summary>
    /// Adds an image to the atlas and immediately places it in the optimal position.
    /// The atlas must be initialized before calling this method.
    /// </summary>
    /// <param name="image">The image to add to the atlas</param>
    /// <returns>A Rectangle representing the position and size of the image in the atlas</returns>
    /// <exception cref="ArgumentNullException">Thrown when image is null</exception>
    /// <exception cref="ArgumentException">Thrown when image dimensions are invalid or exceed atlas size</exception>
    /// <exception cref="InvalidOperationException">Thrown when the image cannot fit in the remaining space</exception>
    public Rectangle AddImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        
        var imageSize = image.Size;
        
        // Validate image size
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
            throw new ArgumentException("Image dimensions must be positive", nameof(image));
        
        if (!_isInitialized)
        {
            _images.Add(new Rectangle(-1, -1, imageSize.Width, imageSize.Height));
            return Rectangle.Empty;
        }

        // Pre-check if image is too large for atlas
        if (imageSize.Width > Width || imageSize.Height > Height)
        {
            throw new ArgumentException($"Image size {imageSize.Width}x{imageSize.Height} exceeds atlas dimensions {Width}x{Height}", nameof(image));
        }

        if (!CanFit(imageSize.Width, imageSize.Height))
        {
            throw new InvalidOperationException($"Cannot add image of size {imageSize.Width}x{imageSize.Height} to atlas. Atlas is full or image is too large for remaining space.");
        }

        var position = FindBestFit(imageSize.Width, imageSize.Height);
        
        if (position.X == -1 || position.Y == -1)
        {
            throw new InvalidOperationException($"Failed to find space for image of size {imageSize.Width}x{imageSize.Height} in atlas.");
        }

        var usedRect = new Rectangle(position.X, position.Y, imageSize.Width, imageSize.Height);
        _images.Add(usedRect);
        
        SplitRectangle(usedRect);
        
        return usedRect;
    }

    /// <summary>
    /// Removes an image from the atlas and frees up its space for reuse.
    /// </summary>
    /// <param name="image">The image to remove from the atlas</param>
    /// <returns>true if the image was found and removed; otherwise, false</returns>
    /// <exception cref="ArgumentNullException">Thrown when image is null</exception>
    public bool RemoveImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        
        var imageSize = image.Size;
        var index = _images.FindIndex(rect => rect.Width == imageSize.Width && rect.Height == imageSize.Height);

        if (index < 0) return false;
        
        var imageRect = _images[index];
        _images.RemoveAt(index);
            
        // Add the freed space back to free rectangles
        if (imageRect.X == -1 || imageRect.Y == -1) return true;
        _freeRectangles.Add(imageRect);
        PruneFreeRectangles();

        return true;
    }

    /// <summary>
    /// Gets the locations of all images in the atlas.
    /// </summary>
    /// <returns>A read-only list of <see cref="Vector2i"/> representing the position of each image</returns>
    public IReadOnlyList<Vector2i> GetImageLocations() => _images.Select(rect => new Vector2i(rect.X, rect.Y)).ToList().AsReadOnly();

    /// <summary>
    /// Gets all image rectangles in the atlas.
    /// </summary>
    /// <returns>A read-only list of <see cref="Rectangle"/> representing the position and size of each image</returns>
    public IReadOnlyList<Rectangle> GetImageRectangles() => _images.AsReadOnly();
}