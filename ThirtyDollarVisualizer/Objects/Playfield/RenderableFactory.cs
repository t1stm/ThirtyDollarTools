using System.Collections.Concurrent;
using OpenTK.Mathematics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Helpers.Textures;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class RenderableFactory(PlayfieldSettings settings, FontFamily font_family)
{
    private readonly ConcurrentDictionary<string, Texture> GeneratedTextures = new();
    private readonly ConcurrentDictionary<string, Texture> MissingValues = new();
    private readonly ConcurrentDictionary<string, Texture> CustomValues = new();

    public readonly Font ValueFont = font_family.CreateFont(settings.ValueFontSize, FontStyle.Bold);
    public readonly Font VolumeFont = font_family.CreateFont(settings.VolumeFontSize, FontStyle.Bold);
    public readonly Color VolumeColor = new Rgba32(204, 204, 204, 1f);
    
    public SoundRenderable? GenerateFrom(BaseEvent base_event)
    {
        var event_name = base_event.SoundEvent;
        if (event_name is null) return null;
        
        var event_texture = 
            TextureDictionary.GetDownloadedAsset(settings.DownloadLocation, event_name);

        var individual_cut_event = base_event is IndividualCutEvent;
        var missing_texture = event_texture is null && !individual_cut_event;
        
        var sound = new SoundRenderable(individual_cut_event ? 
            TextureDictionary.GetICutEventTexture() : 
            event_texture ?? TextureDictionary.GetMissingTexture())
        {
            Value = missing_texture ? GetMissingValue(base_event) : GetValue(base_event),
            Volume = GetVolume(base_event),
            Pan = GetPan(base_event),
            IsDivider = base_event.SoundEvent is "!divider"
        };

        sound.UpdateChildren();
        return sound;
    }

    private TexturedPlane GetMissingValue(BaseEvent base_event)
    {
        var event_name = base_event.SoundEvent;
        var text = $"{event_name}";

        if (base_event.Value != 0) text += $"@{base_event.Value}";
        var texture = MissingValues.GetOrAdd(text, name => new Texture(ValueFont, name));
        return new TexturedPlane(texture);
    }

    private TexturedPlane? GetValue(BaseEvent base_event)
    {
        var value = base_event.Value.ToString("0.##");
        if (base_event.Value > 0 && !(base_event.SoundEvent!.StartsWith('!') || base_event.SoundEvent!.StartsWith('_'))) value = "+" + value;

        value = base_event.ValueScale switch
        {
            ValueScale.Add => "+" + value,
            ValueScale.Times => "×" + value,
            ValueScale.Divide => "/" + value,
            _ => value
        };
        
        Texture? value_texture = null;

        switch (base_event)
        {
            case IndividualCutEvent ice:
            {
                var cut_sounds = ice.CutSounds.ToArray();
                var joined = string.Join('|', cut_sounds);

                value_texture = CustomValues.GetOrAdd(joined, _ =>
                {
                    var available_textures =
                        cut_sounds.Where(r => File.Exists($"{settings.DownloadLocation}/Images/{r}.png"));

                    var textures = available_textures.Select(t => new Texture($"{settings.DownloadLocation}/Images/{t}.png")).ToArray();
                    return  new Texture(textures, 2, settings.RenderScale);
                });
                
                break;
            }

            case { SoundEvent: "!bg" }:
            {
                var parsed_value = (long)base_event.Value;
                var seconds = (parsed_value >> 24) / 1000f;
                value = seconds.ToString("0.##");

                var r = (byte)parsed_value;
                var g = (byte)(parsed_value >> 8);
                var b = (byte)(parsed_value >> 16);

                var texture_id = $"({r},{g},{b}) {value}s";

                value_texture =
                    CustomValues.GetOrAdd(texture_id, _ => new Texture(ValueFont, new Rgb24(r, g, b), value));
                break;
            }

            case { SoundEvent: "!volume" }:
            {
                if (base_event.ValueScale is not ValueScale.Times and not ValueScale.Divide)
                    value += "%";
                break;
            }

            case { SoundEvent: "!pulse" }:
            {
                var parsed_value = (long)base_event.Value;
                var repeats = (byte)parsed_value;
                var pulse_times = (short)(parsed_value >> 8);

                value = $"{repeats}, {pulse_times}";
                break;
            }
        }
        
        if ((value_texture == null && base_event.Value != 0 && base_event.SoundEvent is not "_pause") ||
            base_event.SoundEvent is "!transpose")
        {
            value_texture = GeneratedTextures.GetOrAdd(value, _ => new Texture(ValueFont, value));
        }

        return value_texture is null ? null : 
            new TexturedPlane(value_texture);
    } 
    
    private TexturedPlane? GetVolume(BaseEvent base_event)
    {
        if (base_event.Volume is null or 100d) return null;

        var volume = base_event.Volume.Value;
        var volume_text = volume.ToString("0.#") + "%";

        var volume_texture = GeneratedTextures.GetOrAdd(volume_text, _ => new Texture(VolumeFont, volume_text, VolumeColor));
        return new TexturedPlane(volume_texture);
    }
    
    private TexturedPlane? GetPan(BaseEvent base_event)
    {
        if (base_event is not PannedEvent panned_event) return null;
        
        var pan = panned_event.Pan;
        if (pan == 0f) return null;
        
        var pan_text = Math.Abs(pan).ToString(".#");

        switch (pan)
        {
            case < 0:
                pan_text += "|";
                break;

            case > 0:
                pan_text = "|" + pan_text;
                break;
        }
            
        var pan_texture = GeneratedTextures.GetOrAdd(pan_text, _ => new Texture(VolumeFont, pan_text));
        return new TexturedPlane(pan_texture);
    }
}