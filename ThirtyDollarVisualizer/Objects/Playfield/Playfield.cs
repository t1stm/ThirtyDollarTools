using System.Collections.Concurrent;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Objects;

public class Playfield(PlayfieldSettings settings)
{
    public Memory<SoundRenderable?> Objects = Memory<SoundRenderable?>.Empty;
    private readonly LayoutHandler LayoutHandler = new(64 * settings.RenderScale, 16, 
        new GapBox(6f), new GapBox(15f, 15f));
    private readonly DollarStoreCamera TemporaryCamera = new(Vector3.Zero, Vector2i.Zero);
    private readonly ColoredPlane ObjectBox = new((0, 0, 0, 0.25f), (0, 0, 0), (0, 0, 0));
    private readonly ConcurrentDictionary<string, Texture> DecreasingValuesDictionary = new();
    
    private byte[] AnimatedAssets = Array.Empty<byte>(); // TODO hyped up for this.
    private int DividerCount;

    public async Task UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;
        var sounds = new SoundRenderable?[events.Length];
        
        var factory = new RenderableFactory(settings, Fonts.GetFontFamily());

        await Parallel.ForAsync(0, events.Length, (i, token) =>
        {
            if (token.IsCancellationRequested) return ValueTask.FromCanceled(token);
            
            var ev = events[i];
            if (string.IsNullOrEmpty(ev.SoundEvent) ||
                (ev.SoundEvent.StartsWith('#') && ev is not ICustomActionEvent) || ev is IHiddenEvent) return ValueTask.CompletedTask;

            sounds[i] = factory.GenerateFrom(ev);
            return ValueTask.CompletedTask;
        });

        LayoutHandler.Reset();

        for (var i = 0; i < sounds.Length; i++)
        {
            var sound = sounds[i];
            if (sound is null) continue;
            PositionSound(LayoutHandler, in sound);

            if (!sound.IsDivider || i + 1 >= sounds.Length) continue;

            LayoutHandler.NewLine(2);
            DividerCount++;
        }

        LayoutHandler.Finish();

        var decreasing_events = events.Where(r => r.SoundEvent is "!stop" or "!loopmany");

        foreach (var e in decreasing_events)
        {
            var textures = e.Value;
            for (var val = textures; val >= 0; val--)
            {
                var search = val.ToString("0.##");
                if (DecreasingValuesDictionary.ContainsKey(search)) continue;

                var texture = new Texture(factory.ValueFont, search);
                DecreasingValuesDictionary.GetOrAdd(search, texture);
            }
        }
        
        DecreasingValuesDictionary.GetOrAdd("0", _ => new Texture(factory.ValueFont, "0"));
        Objects = sounds;
    }

    private static void PositionSound(LayoutHandler layout_handler, in SoundRenderable sound)
    {
        var texture = sound.GetTexture();
        var texture_x = texture?.Width ?? 0;
        var texture_y = texture?.Height ?? 0;

        var aspect_ratio = (float)texture_x / texture_y;
        Vector2 box_scale = (layout_handler.Size, layout_handler.Size);
        Vector2 wanted_scale = (layout_handler.Size, layout_handler.Size);
        
        switch (aspect_ratio)
        {
            case > 1:
                wanted_scale.Y = layout_handler.Size / aspect_ratio;
                break;
            case < 1:
                wanted_scale.X = layout_handler.Size * aspect_ratio;
                break;
        }
        
        sound.SetScale((wanted_scale.X, wanted_scale.Y, 0));
        
        var real_position = layout_handler.GetNewPosition(wanted_scale);
        var texture_position = (real_position.X, real_position.Y);
        
        var delta_x = layout_handler.Size - wanted_scale.X;
        var delta_y = layout_handler.Size - wanted_scale.Y;

        texture_position.X += delta_x / 2f;
        texture_position.Y += delta_y / 2f;
        
        sound.SetPosition((texture_position.X, texture_position.Y, 0));

        var bottom_center = real_position + (box_scale.X / 2f, box_scale.Y);
        var top_right = real_position + (box_scale.X + 6f, 0f);

        sound.Value?.SetPosition((bottom_center.X, bottom_center.Y, 0), PositionAlign.Center);
        sound.Volume?.SetPosition((top_right.X, top_right.Y, 0), PositionAlign.TopRight);
        sound.Pan?.SetPosition((real_position.X, real_position.Y, 0));
    }

    public void Render(DollarStoreCamera real_camera, float zoom)
    {
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
        var dividers_size = size_renderable * DividerCount;
        
        // fix values when the zoom is changed
        camera_y -= height_scale;
        camera_yh += height_scale;
        
        // render the background dimmed box
        ObjectBox.SetPosition((0,0,0));
        ObjectBox.SetScale((LayoutHandler.Width, LayoutHandler.Height, 0));
        ObjectBox.BorderRadius = 0;
        
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

            // Bounds checks for viewport.

            if (place.X + scale.X < camera_x || place.X - scale.X > camera_xw) continue;
            if (place.Y + scale.Y < camera_y || place.Y - scale.Y > camera_yh) continue;

            renderable.Render(TemporaryCamera);
        }
    }
}