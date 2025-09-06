using System.Collections.Concurrent;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Helpers.Textures;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Textures;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects;

/// <summary>
///     Creates a SoundRenderable generator.
/// </summary>
/// <param name="settings">The settings to use.</param>
/// <param name="font_family">The current font family.</param>
public class RenderableFactory(PlayfieldSettings settings, FontFamily font_family)
{
    private readonly ConcurrentDictionary<string, Texture> CustomValues = new();
    private readonly ConcurrentDictionary<string, Texture> GeneratedSmallTextures = new();
    private readonly ConcurrentDictionary<string, Texture> GeneratedTextures = new();
    private readonly ConcurrentDictionary<string, Texture> MissingValues = new();

    /// <summary>
    ///     The given font used for value textures.
    /// </summary>
    public readonly Font ValueFont =
        font_family.CreateFont(settings.ValueFontSize * settings.RenderScale, FontStyle.Bold);

    /// <summary>
    ///     The color of the text used in volume textures.
    /// </summary>
    public readonly Color VolumeColor = new Rgba32(204, 204, 204, 1f);

    /// <summary>
    ///     The given font used for volume textures.
    /// </summary>
    public readonly Font VolumeFont =
        font_family.CreateFont(settings.VolumeFontSize * settings.RenderScale, FontStyle.Bold);

    /// <summary>
    ///     Creates a new SoundRenderable from a given Thirty Dollar event.
    /// </summary>
    /// <param name="base_event">A Thirty Dollar event.</param>
    /// <returns>A new SoundRenderable if the event is valid.</returns>
    public SoundRenderable CookUp(BaseEvent base_event)
    {
        var event_name = base_event.SoundEvent;
        ArgumentNullException.ThrowIfNull(event_name);

        // gets the sound's texture
        var event_texture =
            TextureDictionary.GetDownloadedAsset(settings.DownloadLocation, event_name);
        
        AssetTexture texture;
        if (base_event is IndividualCutEvent individualCut)
        {
            texture = individualCut.IsStandardImplementation ? 
                TextureDictionary.GetDownloadedAsset(settings.DownloadLocation, "!cut") ?? TextureDictionary.GetMissingTexture() : 
                TextureDictionary.GetICutEventTexture();
        }
        else
        {
            if (event_texture == null && settings.SampleHolder.StringToSoundReferences.TryGetValue(event_name, out var soundReference))
            {
                event_texture =
                    TextureDictionary.GetDownloadedAsset(settings.DownloadLocation, soundReference.Filename ?? "");
            }
            texture = event_texture ?? TextureDictionary.GetMissingTexture();
        }
        
        // creates the sound
        var sound = new SoundRenderable(texture)
        {
            Value = event_texture is null && base_event is not IndividualCutEvent ? GetMissingValue(base_event) : GetValue(base_event),
            Volume = GetVolume(base_event),
            Pan = GetPan(base_event),
            IsDivider = base_event.SoundEvent is "!divider"
        };

        // sets the "children", that are used in the renderer.
        sound.UpdateChildren();
        return sound;
    }

    /// <summary>
    ///     Generates a custom value texture that contains useful info for the missing sound.
    /// </summary>
    /// <param name="base_event">The sound that doesn't have a texture available.</param>
    /// <returns>The custom value texture.</returns>
    private TexturedPlane GetMissingValue(BaseEvent base_event)
    {
        var event_name = base_event.SoundEvent;
        var text = $"{event_name}";

        if (base_event.Value != 0) text += $"@{base_event.Value}";
        var texture = MissingValues.GetOrAdd(text, name => new FontTexture(ValueFont, name));
        return new TexturedPlane(texture);
    }

    /// <summary>
    ///     Gets the value texture for a given Thirty Dollar event.
    /// </summary>
    /// <param name="base_event">A Thirty Dollar event.</param>
    /// <returns>A value texture plane if applicable.</returns>
    private TexturedPlane? GetValue(BaseEvent base_event)
    {
        // formats the value to a one with two decimal places
        var value = base_event.Value.ToString("0.##");

        // adds a plus to the value for events that are sounds
        if (base_event.Value > 0 && !(base_event.SoundEvent!.StartsWith('!') || base_event.SoundEvent!.StartsWith('_')))
            value = "+" + value;

        // handles the value text for actions that have a value scale
        value = base_event.ValueScale switch
        {
            ValueScale.Add => "+" + value,
            ValueScale.Times => "×" + value,
            ValueScale.Divide => "/" + value,
            _ => value
        };

        Texture? value_texture = null;

        // handles value textures for events that require custom values
        switch (base_event)
        {
            case IndividualCutEvent ice:
            {
                // gets all sounds that are being cut
                var cut_sounds = ice.CutSounds.ToArray();

                // makes a texture ID for them
                var joined = string.Join('|', cut_sounds);

                // gets or creates a new value texture if not created yet
                value_texture = CustomValues.GetOrAdd(joined, _ =>
                {
                    var available_textures =
                        cut_sounds.Select(sound =>
                        {
                            if (File.Exists($"{settings.DownloadLocation}/Images/{sound}.png"))
                                return sound;
                            
                            return settings.SampleHolder.StringToSoundReferences.TryGetValue(sound, out var soundReference) ? soundReference.Filename ?? sound : sound;
                        });

                    var textures = available_textures
                        .Select(texture => new StaticTexture($"{settings.DownloadLocation}/Images/{texture}.png")).ToArray();
                    return new IconFlexTexture(textures, 2, settings.ValueFontSize * settings.RenderScale);
                });

                break;
            }

            case { SoundEvent: "!bg" }:
            {
                // background values are contained by a bitshifted long value
                // casts the double (64 bit floating) value to long (64 bit fixed)
                var parsed_value = (long)base_event.Value;

                // gets the seconds, which are encoded last
                var seconds = (parsed_value >> 32) / 1000f;
                value = seconds.ToString("0.##");

                // gets the RGB values
                var r = (byte)parsed_value;
                var g = (byte)(parsed_value >> 8);
                var b = (byte)(parsed_value >> 16);
                var a = (byte)(parsed_value >> 24);

                // creates a texture ID for the current colors
                var texture_id = $"({r},{g},{b},{a}) {value}s";

                // gets if already exists a texture for the current value, or creates a new one
                value_texture =
                    CustomValues.GetOrAdd(texture_id, _ => new CircleTexture(ValueFont, new Rgba32(r, g, b, a), value));
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
                // pulses are also stored in a custom way, by bitshifting a long value
                // again casting the double to long
                var parsed_value = (long)base_event.Value;

                // the amount of repeats is capped at 255
                var repeats = (byte)parsed_value;

                // the amount of pulses is capped at 32767 
                var pulse_times = (short)(parsed_value >> 8);

                value = $"{repeats}, {pulse_times}";
                break;
            }
        }

        // continues getting or generating a value texture if not set before this check
        if ((value_texture == null && base_event.Value != 0 && base_event.SoundEvent is not "_pause") ||
            base_event.SoundEvent is "!transpose")
            value_texture = GeneratedTextures.GetOrAdd(value, _ => new FontTexture(ValueFont, value));

        // returns a new value texture plane
        return value_texture is null ? null : new TexturedPlane(value_texture);
    }

    /// <summary>
    ///     Gets the volume texture for a given Thirty Dollar event.
    /// </summary>
    /// <param name="base_event">A Thirty Dollar event.</param>
    /// <returns>A volume texture plane if applicable.</returns>
    private TexturedPlane? GetVolume(BaseEvent base_event)
    {
        if (base_event.Volume is null or 100d) return null;

        var volume = base_event.Volume.Value;
        var volume_text = volume.ToString("0.#") + "%";

        var volume_texture =
            GeneratedSmallTextures.GetOrAdd(volume_text, _ => new FontTexture(VolumeFont, volume_text, VolumeColor));
        return new TexturedPlane(volume_texture);
    }

    /// <summary>
    ///     Gets the pan texture for a given Thirty Dollar event.
    /// </summary>
    /// <param name="base_event">A Thirty Dollar event.</param>
    /// <returns>A pan texture plane if applicable.</returns>
    private TexturedPlane? GetPan(BaseEvent base_event)
    {
        if (base_event is not PannedEvent panned_event) return null;
        
        var isStandardImplementation = panned_event.IsStandardImplementation;

        var pan = panned_event.Pan;
        if (pan == 0f) return null;

        // formats the pan to a single decimal
        var pan_text = isStandardImplementation ? 
            Math.Abs(panned_event.TDWPan).ToString("0.#") : 
            Math.Abs(pan).ToString(".#");
        
        switch (pan)
        {
            case < 0:
                pan_text += "|";
                break;

            case > 0:
                pan_text = "|" + pan_text;
                break;
            
            /*case < 0 when isStandardImplementation:
                pan_text += pan_text + "◂";
                break;

            case > 0 when isStandardImplementation:
                pan_text += "▸";
                break;*/
        }

        // gets or generates a new texture
        var pan_texture = GeneratedSmallTextures.GetOrAdd(pan_text, _ => new FontTexture(VolumeFont, pan_text));
        return new TexturedPlane(pan_texture);
    }
}