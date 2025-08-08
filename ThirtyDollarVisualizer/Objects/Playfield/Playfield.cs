using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Text;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;

namespace ThirtyDollarVisualizer.Objects.Playfield;

public class Playfield(PlayfieldSettings settings)
{
    private readonly HashSet<AssetTexture> _animatedTextures = [];

    private readonly List<float> _dividerPositionsY = [];

    private readonly LayoutHandler _layoutHandler = new(settings.SoundSize * settings.RenderScale,
        settings.SoundsOnASingleLine,
        settings.SoundMargin * settings.RenderScale / 2,
        15f * settings.RenderScale);

    private readonly ColoredPlane _objectBox = new()
    {
        Color = (0, 0, 0, 0.25f)
    };

    private readonly DollarStoreCamera _temporaryCamera = new(Vector3.Zero, Vector2i.Zero);

    /// <summary>
    /// Dictionary containing all decreasing value events' textures for this playfield.
    /// </summary>
    public readonly ConcurrentDictionary<string, SingleTexture> DecreasingValuesCache = new();

    private byte[] _animatedAssets = []; // TODO hyped up for this.

    private Vector3 _currentPosition = Vector3.Zero;
    private bool _firstPosition = true;

    private Vector3 _targetPosition = Vector3.Zero;
    public Memory<PlayfieldLine> Lines = Memory<PlayfieldLine>.Empty;

    /// <summary>
    /// Contains all sound renderables.
    /// </summary>
    public List<SoundRenderable> Objects = [];

    public bool DisplayCenter { get; set; } = true;

    public Task UpdateSounds(Sequence sequence)
    {
        // get all events and make an array that will hold their renderable
        var events = sequence.Events;

        // creates a new renderable factory
        var factory = new RenderableFactory(settings, Fonts.GetFontFamily());

        // clear animated texture update cache
        _animatedTextures.Clear();

        // using multiple threads to make each of the renderables
        var sounds = events
            .AsParallel()
            .Where(ev => !IsEventHidden(ev))
            .Select(ev => factory.CookUp(ev))
            .ToList();

        // prepare rendering stuff
        _layoutHandler.Reset();
        _dividerPositionsY.Clear();

        // position every sound using the layout handler
        for (var i = 0; i < sounds.Count; i++)
        {
            var sound = sounds[i];

            // add the sound's texture to a cache if animated
            var texture = sound.GetTexture();
            if (texture is AssetTexture { IsAnimated: true } asset_texture) _animatedTextures.Add(asset_texture);

            PositionSound(_layoutHandler, in sound);

            // check if sound is a divider. if not, continue to the next sound.
            if (!sound.IsDivider || i + 1 >= sounds.Count) continue;

            // add the current divider's position for render culling
            _dividerPositionsY.Add(sound.Position.Y);

            _layoutHandler.NewLine(
                // edge case when a new line was already created
                // by the !divider object being on the end of the line
                _layoutHandler.CurrentSoundIndex == 0 ? 1 : 2);
        }

        // add bottom padding to the layout
        _layoutHandler.Finish();

        var lines = sounds.GroupBy(r => r.OriginalY, (_, enumerable) =>
        {
            var sound_renderables = enumerable as SoundRenderable[] ?? enumerable.ToArray();
            return new PlayfieldLine(settings.SoundsOnASingleLine)
            {
                Sounds = sound_renderables.ToArray(),
                Count = sound_renderables.Length
            };
        }).ToArray();

        // generate textures for all decreasing events
        var decreasing_events = events.Where(r => r.SoundEvent is "!stop" or "!loopmany");
        foreach (var e in decreasing_events)
        {
            var textures = e.Value;
            for (var val = textures; val >= 0; val--)
            {
                var search = val.ToString("0.##");
                DecreasingValuesCache.GetOrAdd(search, _ => new FontTexture(factory.ValueFont, search));
            }
        }

        // add default 0 texture
        DecreasingValuesCache.GetOrAdd("0", _ => new FontTexture(factory.ValueFont, "0"));

        // sets objects to be used by the render method
        Objects = sounds;
        Lines = lines;

        return Task.CompletedTask;
    }

    public static bool IsEventHidden(BaseEvent ev)
    {
        return string.IsNullOrEmpty(ev.SoundEvent) || (ev.SoundEvent.StartsWith('#') && ev is not ICustomActionEvent) ||
               ev is IHiddenEvent;
    }

    private static void PositionSound(LayoutHandler layoutHandler, in SoundRenderable sound)
    {
        // get the current sound's texture information
        var texture = sound.GetTexture();
        var texture_x = texture?.Width ?? 0;
        var texture_y = texture?.Height ?? 0;

        // get the aspect ratio for events without an equal size
        var aspect_ratio = (float)texture_x / texture_y;

        // box scale is the maximum size a sound should cover
        Vector2 box_scale = (layoutHandler.Size, layoutHandler.Size);
        // wanted scale is the corrected size by the aspect ratio
        Vector2 wanted_scale = (layoutHandler.Size, layoutHandler.Size);

        // handle aspect ratio corrections
        switch (aspect_ratio)
        {
            case > 1:
                wanted_scale.Y = layoutHandler.Size / aspect_ratio;
                break;
            case < 1:
                wanted_scale.X = layoutHandler.Size * aspect_ratio;
                break;
        }

        // set the size of the sound's texture to the wanted size
        sound.Scale = (wanted_scale.X, wanted_scale.Y, 0);

        // calculates the wanted position to avoid stretching of the texture
        var box_position = layoutHandler.GetNewPosition();
        var texture_position = (box_position.X, box_position.Y);

        var delta_x = layoutHandler.Size - wanted_scale.X;
        var delta_y = layoutHandler.Size - wanted_scale.Y;

        texture_position.X += delta_x / 2f;
        texture_position.Y += delta_y / 2f;

        sound.SetPosition((texture_position.X, texture_position.Y, 0));

        // position value, volume, pan to their box locations
        var bottom_center = box_position + (box_scale.X / 2f, box_scale.Y);
        var top_right = box_position + (box_scale.X + 6f, 0f);

        sound.Value?.SetPosition((bottom_center.X, bottom_center.Y, 0), PositionAlign.Center);
        sound.Volume?.SetPosition((top_right.X, top_right.Y, 0), PositionAlign.TopRight);
        sound.Pan?.SetPosition((box_position.X, box_position.Y, 0));
        sound.OriginalY = box_position.Y;
    }

    public void Render(DollarStoreCamera realCamera, float zoom, float updateDelta)
    {
        // update animated textures
        foreach (var texture in _animatedTextures) texture.Update();

        // avoid doing modifications to the main camera
        _temporaryCamera.CopyFrom(realCamera);

        // offset temporary camera to enable positioning anywhere
        var left_margin = (_temporaryCamera.Width - _layoutHandler.Width) / 2f;

        _targetPosition = DisplayCenter ? (-left_margin, 0, 0) : Vector3.Zero;
        if (_firstPosition)
        {
            _currentPosition = _targetPosition;
            _firstPosition = false;
        }

        _temporaryCamera.SetOffset(_currentPosition =
            SteppingFunctions.Exponential(_currentPosition, _targetPosition, updateDelta));
        _temporaryCamera.UpdateMatrix();

        // get generic camera values
        var camera_height = _temporaryCamera.Height;

        // set render culling limits
        var clamped_scale = Math.Min(zoom, 1f);
        var height_scale = camera_height / clamped_scale - camera_height;

        // get render culling camera values
        var camera_y = _temporaryCamera.Position.Y;
        var camera_yh = camera_y + camera_height;

        // get render culling object values
        var size_renderable = _layoutHandler.Size;
        var vertical_margin = _layoutHandler.VerticalMargin;

        // fix values when the zoom is changed
        camera_y -= height_scale / 2;
        camera_yh += height_scale / 2;

        // gets the number of dividers at the top and bottom of the screen
        var top_camera_dividers = 0;
        var bottom_camera_dividers = 0;

        // explicitly convert the list to a span for faster iteration
        foreach (var position in CollectionsMarshal.AsSpan(_dividerPositionsY))
        {
            if (position <= camera_y + size_renderable)
                top_camera_dividers++;

            if (position <= camera_yh)
                bottom_camera_dividers++;
        }

        // render the background dimmed box
        _objectBox.SetPosition((0, 0, 0));
        _objectBox.Scale = (_layoutHandler.Width, _layoutHandler.Height, 0);
        _objectBox.BorderRadius = 0f;

        _objectBox.UpdateModel(false);
        _objectBox.Render(_temporaryCamera);

        // get all lines this playfield has
        var tdw_span = Lines.Span;

        // finds the index of the line at the start of the view
        var start_line = camera_y / (size_renderable + vertical_margin);
        var start_index = start_line - top_camera_dividers - 1;

        // same but for the end of the view
        var end_line = camera_yh / (size_renderable + vertical_margin);
        var end_index = end_line - bottom_camera_dividers + 1;

        var start = (int)Math.Max(0, Math.Floor(start_index));
        var end = (int)Math.Min(tdw_span.Length, Math.Ceiling(end_index));

        // renders all lines in the start - end range
        int i;
        for (i = start; i < end; i++)
        {
            var line = tdw_span[i];
            line.Render(_temporaryCamera);
        }
    }
}