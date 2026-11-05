using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.Texture;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Renderer.Textures;
using Sundex.Style.DSL;
using ImageData = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace Sundex.Components.Panels;

/// <summary>How a texture is mapped onto the element's box.</summary>
public enum TextureFit
{
    /// <summary>Fill the box exactly, ignoring the texture's aspect ratio.</summary>
    Stretch,

    /// <summary>Scale down to fit inside the box, keeping the aspect ratio and centering the result.</summary>
    Fit
}

/// <summary>
///     A panel whose background is a single <see cref="TexturedPlane" />, fed by
///     <see cref="Src" /> through the normal <c>AssetProvider</c> loading path. The fetch and
///     decode run on <c>AssetProvider.ThreadRunner</c>, so a slow disk or a remote URL never
///     stalls a frame. A failed load puts the element in its <c>state[errored]</c> block, calls
///     <see cref="OnError" /> and logs a warning; with no rule to say otherwise it then shows a
///     bundled placeholder.
///     <para>
///         <see cref="Src" />, <see cref="Storage" /> and <see cref="TextureFit" /> are all
///         [NamedSetting]s: they can come from markup attributes, from a stylesheet rule, from a
///         state override, or be assigned at runtime, and any of those may change at any time.
///     </para>
/// </summary>
public class Image : Panel
{
    /// <summary>Lucide's image-off, embedded in this assembly.</summary>
    private const string PlaceholderLocation = "Assets/Textures/image-invalid.png";

    /// <summary>The placeholder never draws larger than this, so a wide frame does not blow it up.</summary>
    private const float PlaceholderSize = 48;

    private readonly TexturedPlane _plane;

    /// <summary>
    ///     Bumped on every load request. A worker compares it before publishing, so a fetch
    ///     that a newer src has already superseded is dropped instead of landing last.
    /// </summary>
    private int _generation;

    /// <summary>
    ///     Set while a style pass is running, so the several [NamedSetting] writes it makes
    ///     (src before storage, say - reflection order is not declaration order) coalesce into
    ///     one fetch with the final values rather than firing one per property.
    /// </summary>
    private bool _deferLoad;

    private bool _loadDeferred;

    /// <summary>A worker's failure, waiting to be handled on the update thread. Same hand-off as <see cref="_pending" />.</summary>
    private LoadFailure? _failure;

    /// <summary>Set while a load leaving the error state runs its style pass: the caller is about to name what loads.</summary>
    private bool _holdLoad;

    private bool _loadingPlaceholder;

    /// <summary>Decoded pixels waiting to be turned into a GPU texture on the update thread.</summary>
    private ImageData? _pending;

    /// <summary>
    ///     The pixels backing <see cref="_texture" />. Held because
    ///     <see cref="GPUTexture.QueueUploadToGPU{TPixel}" /> pins this memory and only reads it
    ///     at the first <c>Bind()</c>, which happens on the render thread some frames later.
    /// </summary>
    private ImageData? _pixels;

    private string _requestedSrc = string.Empty;
    private StorageLocation _requestedStorage = StorageLocation.Unknown;
    private GPUTexture? _texture;

    public Image(UIContext context) : base(context)
    {
        // ponytail: the texture IS the Background, so Panel already handles queueing,
        // dequeue-on-StopRendering, clipping, border-radius and render-layer swaps. The
        // cost is that a stylesheet `component image { background = ... }` rule would
        // replace the plane; give Image its own renderable slot if a tint behind a
        // transparent PNG is ever needed.
        Background = _plane = new TexturedPlane { IsVisible = false };

        // Registered for good, not just while loading: the update pass is the only
        // reliable per-frame hook, and it is where a finished fetch is handed to GL.
        Context.RegisterUpdate(this);
    }

    public override string Tag => "image";

    /// <summary>
    ///     Where to load the image from - an assembly resource, a path on disk, or an
    ///     <c>http(s)://</c> URL. Assigning a new value starts a fresh load and supersedes any
    ///     fetch still in flight.
    /// </summary>
    [NamedSetting("src")]
    public string Src
    {
        get;
        set
        {
            if (field == value) return;
            // Before the write: leaving the state restores the base snapshot over this property.
            ClearErroredFromCode();
            field = value;
            RequestLoad();
        }
    } = string.Empty;

    /// <summary>
    ///     Storage to load <see cref="Src" /> from. Left at <see cref="StorageLocation.Unknown" />
    ///     the loader picks: a URL goes to the network, anything else is tried on disk and then
    ///     in the asset assemblies.
    /// </summary>
    [NamedSetting("storage")]
    public StorageLocation Storage
    {
        get;
        set
        {
            if (field == value) return;
            ClearErroredFromCode();
            field = value;
            RequestLoad();
        }
    } = StorageLocation.Unknown;

    [NamedSetting("texture-fit")]
    public TextureFit TextureFit
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = TextureFit.Stretch;

    /// <summary>
    ///     The widest the decoded pixels may be, 0 for no limit. Only ever shrinks. Set it before
    ///     <see cref="Src" />: the element keeps its decoded pixels for the texture's lifetime, so a
    ///     large image shown small otherwise costs its full size in memory, twice.
    /// </summary>
    public int DecodeWidth { get; set; }

    /// <summary>
    ///     Called on the update thread when a load fails, once per failed load: <see cref="Src" />,
    ///     and then a <c>state[errored]</c> rule's own <c>src</c> if that fails too.
    /// </summary>
    public Action<Image, Exception>? OnError { get; set; }

    /// <summary>True while the texture on show is the bundled placeholder rather than <see cref="Src" />.</summary>
    public bool ShowsPlaceholder { get; private set; }

    /// <summary>The fetch currently in flight, if any. Await it in tests instead of polling.</summary>
    public Task? LoadTask { get; private set; }

    /// <summary>
    ///     How many fetches this element has started. A style pass that writes src from
    ///     several rules still counts as one, so a remote src makes a single request.
    /// </summary>
    public int LoadCount { get; private set; }

    /// <summary>The texture in use, once a load has landed and been applied on the update thread.</summary>
    public GPUTexture? Texture => _texture;

    /// <summary>True once <see cref="Src" /> has been fetched, decoded and handed to GL.</summary>
    public bool IsLoaded => _texture is not null && !ShowsPlaceholder;

    /// <summary>
    ///     Keeps this element in the update set unconditionally. The base implementation drops
    ///     elements with no animations, which would strand a fetch that finishes afterwards.
    /// </summary>
    public override void UpdateAnimationRegistrationState()
    {
        Context.RegisterUpdate(this);
    }

    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        _deferLoad = true;
        try
        {
            base.ApplyStyleSheet(styleSheet);
        }
        finally
        {
            _deferLoad = false;
        }

        FlushDeferredLoad();
    }

    public override void ApplyStateOverride(StyleSheet styleSheet, string state)
    {
        _deferLoad = true;
        try
        {
            base.ApplyStateOverride(styleSheet, state);
        }
        finally
        {
            _deferLoad = false;
        }

        FlushDeferredLoad();
    }

    public override void InvalidateStyle()
    {
        _deferLoad = true;
        try
        {
            base.InvalidateStyle();
        }
        finally
        {
            _deferLoad = false;
        }

        FlushDeferredLoad();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);

        // Exchange, not read-then-clear: a worker publishing between the two would be lost.
        if (Interlocked.Exchange(ref _pending, null) is { } pixels) ApplyTexture(pixels);
        // A fetch superseded after the worker's own check must not fail the one that replaced it.
        if (Interlocked.Exchange(ref _failure, null) is { } failure &&
            failure.Generation == Volatile.Read(ref _generation))
            HandleFailure(failure.Exception);
    }

    /// <summary>
    ///     Uploads decoded pixels as this image's texture. Called by the loader once the fetch
    ///     lands; public so a caller that already holds an image can skip the asset path.
    ///     Must run on the update thread.
    /// </summary>
    public void ApplyTexture(ImageData pixels)
    {
        var texture = new GPUTexture
        {
            Width = pixels.Width,
            Height = pixels.Height
        };

        // Neither call touches GL - the handle is created and the queue drained at the first
        // Bind(), on the render thread.
        texture.QueueUploadToGPU(pixels.Frames[0]);
        SetTexture(texture, pixels);
    }

    /// <summary>
    ///     Swaps in an already-built texture, releasing the previous one. Must run on the
    ///     update thread.
    /// </summary>
    public void SetTexture(GPUTexture texture, ImageData? pixels = null)
    {
        ReleaseTexture();

        ShowsPlaceholder = _loadingPlaceholder;
        _loadingPlaceholder = false;
        Context.AssetProvider.ThreadRunner.Logger.Debug("Image landed: {Src}", ShowsPlaceholder ? PlaceholderLocation : Src);
        _texture = texture;
        _pixels = pixels;
        _plane.Texture = texture;
        // Held false until here: TexturedPlane.Render does a null-safe Texture?.Bind(), so a
        // plane without one would draw with whatever texture happened to be bound last.
        _plane.IsVisible = true;

        RelayoutFromRoot();
    }

    /// <summary>Drops the current texture, leaving the element laid out but unpainted.</summary>
    public void ClearTexture()
    {
        if (_texture is null) return;
        ReleaseTexture();
        RelayoutFromRoot();
    }

    /// <inheritdoc />
    /// <remarks>
    ///     An auto-sized image takes the texture's pixel dimensions, so
    ///     <c>&lt;image src="..."/&gt;</c> with no width/height sizes itself - and stays
    ///     zero-sized, i.e. invisible, until the bytes land. With a fixed width and an auto height
    ///     the height follows the texture's aspect instead.
    /// </remarks>
    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var (width, height) = base.Measure(parentWidth, parentHeight);
        if (_texture is not { } texture) return (width, height);

        if (ShowsPlaceholder)
        {
            if (Width.Auto) width = PlaceholderSize;
            if (Height.Auto) height = PlaceholderSize;
            return (width, height);
        }

        if (Height.Auto)
            height = Width.Auto || texture.Width <= 0
                ? texture.Height
                : (ParentAssignedWidth ?? width) * texture.Height / texture.Width;
        if (Width.Auto) width = texture.Width;
        return (width, height);
    }

    protected override void DoLayout()
    {
        // Lays the plane out over the whole box, which is exactly Stretch.
        base.DoLayout();

        if ((TextureFit != TextureFit.Fit && !ShowsPlaceholder) ||
            _texture is not { Width: > 0, Height: > 0 } texture) return;

        var boxWidth = Computed.Width;
        var boxHeight = Computed.Height;
        if (boxWidth <= 0 || boxHeight <= 0) return;

        var scale = Math.Min(boxWidth / texture.Width, boxHeight / texture.Height);
        if (ShowsPlaceholder) scale = Math.Min(scale, PlaceholderSize / Math.Max(texture.Width, texture.Height));
        var width = texture.Width * scale;
        var height = texture.Height * scale;

        _plane.Scale = new Vector3(width, height, 1);
        _plane.SetPosition(new Vector3(
            Computed.AbsoluteX + (boxWidth - width) / 2f,
            Computed.AbsoluteY + (boxHeight - height) / 2f,
            0));
    }

    private void RequestLoad()
    {
        if (_deferLoad)
        {
            _loadDeferred = true;
            return;
        }

        StartLoad();
    }

    private void FlushDeferredLoad()
    {
        if (!_loadDeferred || _holdLoad) return;
        StartLoad();
    }

    /// <summary>Code naming something new to load ends the failure. Style passes never do.</summary>
    private void ClearErroredFromCode()
    {
        if (_deferLoad || !Errored) return;

        _holdLoad = true;
        Errored = false;
        _holdLoad = false;
        _loadDeferred = false;
    }

    private void StartLoad()
    {
        _loadDeferred = false;

        var src = Src;
        var storage = Storage;
        if (src == _requestedSrc && storage == _requestedStorage) return;

        _requestedSrc = src;
        _requestedStorage = storage;
        _loadingPlaceholder = false;

        // Invalidates any fetch already running, whether or not a new one starts.
        var generation = Interlocked.Increment(ref _generation);

        if (string.IsNullOrEmpty(src))
        {
            Interlocked.Exchange(ref _pending, null)?.Dispose();
            ClearTexture();
            return;
        }

        LoadCount++;
        Fetch(src, storage, DecodeWidth, generation);
    }

    private void Fetch(string src, StorageLocation storage, int maxWidth, int generation)
    {
        LoadTask = Context.AssetProvider.ThreadRunner.RunTask(() =>
        {
            try
            {
                var holder = Context.AssetProvider.Load<TextureHolder, TextureInfo>(new TextureInfo
                {
                    MaxWidth = maxWidth,
                    AssetInfo = new AssetInfo
                    {
                        Location = src,
                        Storage = storage
                    }
                });

                if (Volatile.Read(ref _generation) != generation)
                {
                    holder.Texture.Dispose();
                    return;
                }

                // Superseding a decode that hasn't been picked up yet - drop it, not this one.
                Interlocked.Exchange(ref _pending, holder.Texture)?.Dispose();
            }
            catch (Exception e)
            {
                Interlocked.Exchange(ref _failure, new LoadFailure(e, generation));
            }
        });
    }

    /// <summary>
    ///     Runs on the update thread. The state goes first, so its rules have applied by the time
    ///     <see cref="OnError" /> looks. It cannot loop: a <c>state[errored]</c> <c>src</c> that fails
    ///     lands back here with nothing new to load and takes the placeholder, and a placeholder
    ///     that fails ends there.
    /// </summary>
    private void HandleFailure(Exception failure)
    {
        var logger = Context.AssetProvider.ThreadRunner.Logger;
        if (_loadingPlaceholder)
        {
            _loadingPlaceholder = false;
            logger.Warning(failure, "The invalid-image placeholder failed to load.");
            return;
        }

        logger.Warning(failure, "Failed to load image {Src}.", Src);

        var failedSrc = Src;
        var failedStorage = Storage;
        ClearTexture();
        Errored = true;
        OnError?.Invoke(this, failure);

        // The state named something else to load, or a fill to show instead of a texture.
        if (Src != failedSrc || Storage != failedStorage || !ReferenceEquals(Background, _plane)) return;

        _loadingPlaceholder = true;
        Fetch(PlaceholderLocation, StorageLocation.Assembly, 0, Interlocked.Increment(ref _generation));
    }

    private void ReleaseTexture()
    {
        if (_texture is { } old)
        {
            if (old.Handle != 0) Context.DeleteQueue.Enqueue(DeleteType.Texture, old.Handle);

            // The queued upload still points at these pixels until the first Bind() runs it;
            // disposing beforehand would hand GL freed memory. Left to the GC in that case.
            if (!old.BufferState.HasFlag(BufferState.PendingUpload)) _pixels?.Dispose();
        }

        _texture = null;
        _pixels = null;
        ShowsPlaceholder = false;
        _plane.Texture = null;
        _plane.IsVisible = false;
    }

    /// <summary>
    ///     Re-runs layout from the root so a newly landed texture's size reaches the parents
    ///     that size around it. Nothing else re-runs layout after an async load: DrawTo happens
    ///     once at build time and Layout() only on resize.
    /// </summary>
    private void RelayoutFromRoot()
    {
        InvalidateLayout();

        UIElement root = this;
        while (root.Parent is { } parent) root = parent;
        root.Layout();
    }

    private sealed record LoadFailure(Exception Exception, int Generation);
}
