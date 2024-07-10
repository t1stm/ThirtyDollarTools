using System.Collections.Concurrent;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Objects;

public class Playfield(PlayfieldSettings settings)
{
    /// <summary>
    /// Contains all sound renderables.
    /// </summary>
    public Memory<SoundRenderable?> Objects = Memory<SoundRenderable?>.Empty;
    
    /// <summary>
    /// Dictionary containing all decreasing value events' textures for this playfield.
    /// </summary>
    public readonly ConcurrentDictionary<string, Texture> DecreasingValuesCache = new();
    
    private readonly LayoutHandler LayoutHandler = new(64 * settings.RenderScale, 16, 
        new GapBox(6f * settings.RenderScale), new GapBox(15f * settings.RenderScale, 15f * settings.RenderScale));
    
    private readonly DollarStoreCamera TemporaryCamera = new(Vector3.Zero, Vector2i.Zero);
    private readonly ColoredPlane ObjectBox = new((0, 0, 0, 0.25f), (0, 0, 0), (0, 0, 0));
    
    private byte[] AnimatedAssets = Array.Empty<byte>(); // TODO hyped up for this.
    private readonly List<float> DividerPositions_Y = new();

    public async Task UpdateSounds(Sequence sequence)
    {
        // get all events and make an array that will hold their renderable
        var events = sequence.Events;
        var sounds = new SoundRenderable?[events.Length];
        
        // creates a new renderable factory
        var factory = new RenderableFactory(settings, Fonts.GetFontFamily());

        // using multiple threads to make each of the renderables
        await Parallel.ForAsync(0, events.Length, (i, token) =>
        {
            // handle cancellation tokens
            if (token.IsCancellationRequested) return ValueTask.FromCanceled(token);
            
            // extract event to local variable
            var base_event = events[i];
            
            // skip all events meant to be invisible
            if (string.IsNullOrEmpty(base_event.SoundEvent) ||
                (base_event.SoundEvent.StartsWith('#') && base_event is not ICustomActionEvent) || 
                base_event is IHiddenEvent) return ValueTask.CompletedTask;
            
            // creates the renderable for the current index
            sounds[i] = factory.CookUp(base_event); // very funny i know
            return ValueTask.CompletedTask;
        });

        // prepare rendering stuff
        LayoutHandler.Reset();
        DividerPositions_Y.Clear();

        // position every sound using the layout handler
        for (var i = 0; i < sounds.Length; i++)
        {
            var sound = sounds[i];
            if (sound is null) continue;
            
            PositionSound(LayoutHandler, in sound);

            // check if sound is a divider. if not continue to the next sound.
            if (!sound.IsDivider || i + 1 >= sounds.Length) continue;

            // add the current divider's position for render culling
            DividerPositions_Y.Add(sound.GetPosition().Y);
            
            LayoutHandler.NewLine(
                // edge case when a new line was already created
                // by the !divider object being on the end of the line
                LayoutHandler.CurrentSoundIndex == 0 ? 1 : 2);
        }

        // add bottom padding to the layout
        LayoutHandler.Finish();

        // generate textures for all decreasing events
        var decreasing_events = events.Where(r => r.SoundEvent is "!stop" or "!loopmany");
        foreach (var e in decreasing_events)
        {
            var textures = e.Value;
            for (var val = textures; val >= 0; val--)
            {
                var search = val.ToString("0.##");
                DecreasingValuesCache.GetOrAdd(search, _ => new Texture(factory.ValueFont, search));
            }
        }
        
        // add default 0 texture
        DecreasingValuesCache.GetOrAdd("0", _ => new Texture(factory.ValueFont, "0"));
        
        // sets objects to be used by the render method
        Objects = sounds;
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
        sound.SetScale((wanted_scale.X, wanted_scale.Y, 0));
        
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
    }

    public void Render(DollarStoreCamera real_camera, float zoom)
    {
        // avoid doing modifications to the main camera
        TemporaryCamera.CopyFrom(real_camera);

        var camera_width = TemporaryCamera.Width;
        var camera_height = TemporaryCamera.Height;
        
        // offset temporary camera to enable positioning anywhere
        var left_margin = (int)(TemporaryCamera.Width / 2f - LayoutHandler.Width / 2f);
        TemporaryCamera.SetOffset((-left_margin, 0, 0));
        TemporaryCamera.UpdateMatrix();
        
        // set render culling limits
        var clamped_scale = Math.Min(zoom, 1f);
        var width_scale = camera_width / clamped_scale - camera_width;
        var height_scale = camera_height / clamped_scale - camera_height;

        // get render culling camera values
        var camera_x = TemporaryCamera.Position.X - width_scale;
        var camera_y = TemporaryCamera.Position.Y;
        var camera_xw = camera_x + camera_width + width_scale;
        var camera_yh = camera_y + camera_height;

        // get render culling object values
        var size_renderable = LayoutHandler.Size + LayoutHandler.HorizontalMargin;
        var repeats_renderable = LayoutHandler.SoundsCount;

        // account for padding between objects
        var padding_rows = repeats_renderable * 2;
        var divider_count = 0;
        
        // disabling dumb resharper errors
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (var position in DividerPositions_Y)
        {
            if (position <= TemporaryCamera.Position.Y) 
                divider_count++;
        }

        var dividers_size = size_renderable * divider_count;
        
        // fix values when the zoom is changed
        camera_y -= height_scale;
        camera_yh += height_scale;
        
        // render the background dimmed box
        ObjectBox.SetPosition((0,0,0));
        ObjectBox.SetScale((LayoutHandler.Width, LayoutHandler.Height, 0));
        ObjectBox.BorderRadius = 0f;
        
        ObjectBox.UpdateModel(false);
        ObjectBox.Render(TemporaryCamera);

        // get all objects this playfield has
        var tdw_span = Objects.Span;
        
        // finds the index of the object at the start of the view
        var start_unclamped = repeats_renderable * (camera_y / size_renderable) - dividers_size - padding_rows;
        var start = (int)Math.Max(0, start_unclamped);

        // same but for the end of the view
        var end_unclamped = repeats_renderable * (camera_y / size_renderable) +
                            repeats_renderable * ((camera_height + height_scale) / size_renderable / clamped_scale) +
                            padding_rows;
        var end = (int)Math.Min(tdw_span.Length, end_unclamped);

        // renders all objects in the start - end range
        for (var i = start; i < end; i++)
        {
            var renderable = tdw_span[i];
            if (renderable is null) continue;

            var position = renderable.GetPosition();
            var translation = renderable.GetTranslation();

            var scale = renderable.GetScale();
            var place = position + translation;

            // Bounds checks for the camera's viewport
            if (place.X + scale.X < camera_x || place.X - scale.X > camera_xw) continue;
            if (place.Y + scale.Y < camera_y || place.Y - scale.Y > camera_yh) continue;

            renderable.Render(TemporaryCamera);
        }
    }
}