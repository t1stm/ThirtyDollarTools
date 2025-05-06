using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Text;
using ThirtyDollarVisualizer.Objects.Textures;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects;

public class Playfield(PlayfieldSettings settings)
{
    /// <summary>
    ///     Dictionary containing all decreasing value events' textures for this playfield.
    /// </summary>
    public readonly ConcurrentDictionary<string, SingleTexture> DecreasingValuesCache = new();
    
    private readonly HashSet<AssetTexture> AnimatedTextures = [];
    
    private readonly List<float> DividerPositions_Y = [];

    private readonly LayoutHandler LayoutHandler = new(settings.SoundSize * settings.RenderScale,
        settings.SoundsOnASingleLine,
        new GapBox(settings.SoundMargin * settings.RenderScale / 2),
        new GapBox(15f * settings.RenderScale, 15f * settings.RenderScale));

    private readonly ColoredPlane ObjectBox = new()
    {
        Color = (0, 0, 0, 0.25f)
    };

    private readonly DollarStoreCamera TemporaryCamera = new(Vector3.Zero, Vector2i.Zero);

    private byte[] AnimatedAssets = []; // TODO hyped up for this.

    private Vector3 CurrentPosition = Vector3.Zero;

    public bool DisplayCenter = true;
    private bool FirstPosition = true;
    public Memory<PlayfieldLine> Lines = Memory<PlayfieldLine>.Empty;

    /// <summary>
    ///     Contains all sound renderables.
    /// </summary>
    public List<SoundRenderable> Objects = [];

    private Vector3 TargetPosition = Vector3.Zero;

    public Task UpdateSounds(Sequence sequence)
    {
        // get all events and make an array that will hold their renderable
        var events = sequence.Events;

        // creates a new renderable factory
        var factory = new RenderableFactory(settings, Fonts.GetFontFamily());
        
        // clear animated texture update cache
        AnimatedTextures.Clear();
        
        // using multiple threads to make each of the renderables
        var sounds = events
            .AsParallel()
            .Where(ev => !IsEventHidden(ev))
            .Select(ev => factory.CookUp(ev))
            .ToList();

        // prepare rendering stuff
        LayoutHandler.Reset();
        DividerPositions_Y.Clear();
        
        // position every sound using the layout handler
        for (var i = 0; i < sounds.Count; i++)
        {
            var sound = sounds[i];
            
            // add the sound's texture to a cache if animated
            var texture = sound.GetTexture();
            if (texture is AssetTexture { IsAnimated: true } asset_texture)
            {
                AnimatedTextures.Add(asset_texture);
            }

            PositionSound(LayoutHandler, in sound);

            // check if sound is a divider. if not continue to the next sound.
            if (!sound.IsDivider || i + 1 >= sounds.Count) continue;

            // add the current divider's position for render culling
            DividerPositions_Y.Add(sound.Position.Y);

            LayoutHandler.NewLine(
                // edge case when a new line was already created
                // by the !divider object being on the end of the line
                LayoutHandler.CurrentSoundIndex == 0 ? 1 : 2);
        }

        // add bottom padding to the layout
        LayoutHandler.Finish();

        var lines = sounds.GroupBy(r => r.Original_Y, (_, enumerable) =>
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
        return string.IsNullOrEmpty(ev.SoundEvent) || (ev.SoundEvent.StartsWith('#') && ev is not ICustomActionEvent) || ev is IHiddenEvent;
    }

    private static void PositionSound(LayoutHandler layout_handler, in SoundRenderable sound)
    {
        // get the current sound's texture information
        var texture = sound.GetTexture();
        var texture_x = texture?.Width ?? 0;
        var texture_y = texture?.Height ?? 0;

        // get aspect ratio for events without equal size
        var aspect_ratio = (float)texture_x / texture_y;

        // box scale is the maximum size a sound should cover
        Vector2 box_scale = (layout_handler.Size, layout_handler.Size);
        // wanted scale is the corrected size by the aspect ratio
        Vector2 wanted_scale = (layout_handler.Size, layout_handler.Size);

        // handle aspect ratio corrections
        switch (aspect_ratio)
        {
            case > 1:
                wanted_scale.Y = layout_handler.Size / aspect_ratio;
                break;
            case < 1:
                wanted_scale.X = layout_handler.Size * aspect_ratio;
                break;
        }

        // set the size of the sound's texture to the wanted size
        sound.Scale = (wanted_scale.X, wanted_scale.Y, 0);

        // calculates the wanted position to avoid stretching of the texture
        var box_position = layout_handler.GetNewPosition();
        var texture_position = (box_position.X, box_position.Y);

        var delta_x = layout_handler.Size - wanted_scale.X;
        var delta_y = layout_handler.Size - wanted_scale.Y;

        texture_position.X += delta_x / 2f;
        texture_position.Y += delta_y / 2f;

        sound.SetPosition((texture_position.X, texture_position.Y, 0));

        // position value, volume, pan to their box locations
        var bottom_center = box_position + (box_scale.X / 2f, box_scale.Y);
        var top_right = box_position + (box_scale.X + 6f, 0f);

        sound.Value?.SetPosition((bottom_center.X, bottom_center.Y, 0), PositionAlign.Center);
        sound.Volume?.SetPosition((top_right.X, top_right.Y, 0), PositionAlign.TopRight);
        sound.Pan?.SetPosition((box_position.X, box_position.Y, 0));
        sound.Original_Y = box_position.Y;
    }

    public void Render(DollarStoreCamera real_camera, float zoom, float update_delta)
    {
        // update animated textures
        foreach (var texture in AnimatedTextures)
        {
            texture.Update();
        }
        
        // avoid doing modifications to the main camera
        TemporaryCamera.CopyFrom(real_camera);

        // offset temporary camera to enable positioning anywhere
        var left_margin = (TemporaryCamera.Width - LayoutHandler.Width) / 2f;

        TargetPosition = DisplayCenter ? (-left_margin, 0, 0) : Vector3.Zero;
        if (FirstPosition)
        {
            CurrentPosition = TargetPosition;
            FirstPosition = false;
        }

        TemporaryCamera.SetOffset(CurrentPosition =
            SteppingFunctions.Exponential(CurrentPosition, TargetPosition, update_delta));
        TemporaryCamera.UpdateMatrix();

        // get generic camera values
        var camera_height = TemporaryCamera.Height;

        // set render culling limits
        var clamped_scale = Math.Min(zoom, 1f);
        var height_scale = camera_height / clamped_scale - camera_height;

        // get render culling camera values
        var camera_y = TemporaryCamera.Position.Y;
        var camera_yh = camera_y + camera_height;

        // get render culling object values
        var size_renderable = LayoutHandler.Size;
        var vertical_margin = LayoutHandler.VerticalMargin;

        // fix values when the zoom is changed
        camera_y -= height_scale / 2;
        camera_yh += height_scale / 2;

        // gets the amount of dividers at the top and bottom of the screen
        var top_camera_dividers = 0;
        var bottom_camera_dividers = 0;

        // disabling dumb resharper errors
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (var position in CollectionsMarshal.AsSpan(DividerPositions_Y))
        {
            if (position <= camera_y + size_renderable)
                top_camera_dividers++;

            if (position <= camera_yh)
                bottom_camera_dividers++;
        }

        // render the background dimmed box
        ObjectBox.SetPosition((0, 0, 0));
        ObjectBox.Scale = (LayoutHandler.Width, LayoutHandler.Height, 0);
        ObjectBox.BorderRadius = 0f;

        ObjectBox.UpdateModel(false);
        ObjectBox.Render(TemporaryCamera);

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
            line.Render(TemporaryCamera);
        }
    }
}