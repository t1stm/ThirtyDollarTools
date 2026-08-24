using System.Runtime.CompilerServices;

namespace VisualizerScene.Settings;

public class VisualizerSettings
{
    /// <summary>
    ///     Fires with the name of the property that just changed. Everything that reads a
    ///     setting into something of its own - a widget's value, a camera, the playfield's
    ///     geometry - listens here instead of taking a copy at construction, so a setting
    ///     written on one screen is live on every other.
    /// </summary>
    public event Action<string>? Changed;

    public int EventSize
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 64;

    public int EventMargin
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 12;

    public int LineAmount
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 16;

    public string Greeting
    {
        get;
        set => SetAndCallModified(out field, value);
    } = "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER";

    public string? AudioBackend
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    public bool TransparentFramebuffer
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    /// <summary>
    ///     Which resampler pitches the samples, by name - see <see cref="Resamplers" />, which
    ///     owns the list and builds the object. A name rather than the object because this is
    ///     what the settings file can hold, and because the two below are its parameters.
    /// </summary>
    public string Resampler
    {
        get;
        set => SetAndCallModified(out field, value);
    } = Resamplers.Hermite;

    /// <summary>Taps per output sample, for <see cref="Resamplers.SincHann" />.</summary>
    public int SincFilterSize
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 64;

    /// <summary>Filter table resolution, for <see cref="Resamplers.SincHann" />.</summary>
    public int SincPrecision
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 512;

    /// <summary>Quantisation steps, for <see cref="Resamplers.ByteCruncher" />.</summary>
    public float CruncherBits
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 64f;

    public bool AutomaticScaling
    {
        get;
        set => SetAndCallModified(out field, value);
    } = true;

    public float ScrollSpeed
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 7.5f;

    public bool UseVsync
    {
        get;
        set => SetAndCallModified(out field, value);
    } = true;

    /// <summary>Whether the loader has already asked about update checking. False means "first run".</summary>
    public bool UpdateCheckAsked
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    public bool CheckForUpdates
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    public bool UpdateIncludePrereleases
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    public bool UpdateIncludeNightlies
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    /// <summary>
    ///     Rebound shortcuts, as "Undo:Ctrl+Z;Redo:Ctrl+Shift+Z" - only what the user changed,
    ///     so it stays empty (and unwritten) until they change something. Parsed by
    ///     <see cref="Keybinds" />, which owns the defaults; see it for why the store is one
    ///     opaque string rather than a property per shortcut.
    /// </summary>
    public string Keybinds
    {
        get;
        set => SetAndCallModified(out field, value);
    } = "";


    private void SetAndCallModified<T>(out T obj, T value, [CallerMemberName] string name = "")
    {
        obj = value;
        Changed?.Invoke(name);
    }
}