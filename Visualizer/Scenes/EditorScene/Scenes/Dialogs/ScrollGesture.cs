using EditorScene.Scenes.Views;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Dialogs;

/// <summary>The instrument editor's tuning fields, in its read-out columns' order.</summary>
public enum TuneField
{
    Value,
    Volume,
    Pan,
    Offset
}

/// <summary>
///     What a wheel notch tunes in the instrument editor, from the held modifiers: plain scroll
///     changes the value by 1, Ctrl+Shift by 0.1, Ctrl the volume by 5 (an empty volume counts
///     as 100), Shift the pan by 10, Alt the offset by 0.01 s. Alt wins over the others.
/// </summary>
public static class ScrollGesture
{
    public const double ValueStep = 1;
    public const double FineValueStep = 0.1;
    public const double VolumeStep = 5;
    public const double PanStep = 10;
    public const double OffsetStep = 0.01;

    /// <summary>A field's plain step: one wheel notch over it, or over a read-out of it.</summary>
    public static double StepOf(TuneField field) => field switch
    {
        TuneField.Volume => VolumeStep,
        TuneField.Pan => PanStep,
        TuneField.Offset => OffsetStep,
        _ => ValueStep
    };

    public static (TuneField Field, double Step) For(bool ctrl, bool shift, bool alt) =>
        alt ? (TuneField.Offset, OffsetStep)
        : ctrl && shift ? (TuneField.Value, FineValueStep)
        : ctrl ? (TuneField.Volume, VolumeStep)
        : shift ? (TuneField.Pan, PanStep)
        : (TuneField.Value, ValueStep);

    /// <summary>Steps one of a sound's fields by <paramref name="delta" /> (notches times the step).</summary>
    public static void Apply(InstrumentSound sound, TuneField field, double delta)
    {
        switch (field)
        {
            case TuneField.Value: sound.Value = StepValue(sound.Value, delta); break;
            case TuneField.Volume: sound.Volume = StepVolume(sound.Volume, delta); break;
            case TuneField.Pan: sound.Pan = StepPan(sound.Pan, delta); break;
            case TuneField.Offset: sound.Offset = StepOffset(sound.Offset, delta); break;
        }
    }

    /// <summary>The same, on the instrument-wide fields.</summary>
    public static void Apply(Instrument instrument, TuneField field, double delta)
    {
        switch (field)
        {
            case TuneField.Value: instrument.Value = StepValue(instrument.Value, delta); break;
            case TuneField.Volume: instrument.Volume = StepVolume(instrument.Volume, delta); break;
            case TuneField.Pan: instrument.Pan = StepPan(instrument.Pan, delta); break;
            case TuneField.Offset: instrument.Offset = StepOffset(instrument.Offset, delta); break;
        }
    }

    // Value and offset are rounded to 4 places so repeated 0.1 and 0.01 steps don't gather
    // float error that a field hides but an export writes out.
    private static double StepValue(double value, double delta) =>
        Math.Round(Math.Clamp(value + delta, -TrackEditorView.MaxValue, TrackEditorView.MaxValue), 4);

    private static double StepVolume(double? volume, double delta) => Math.Clamp((volume ?? 100) + delta, 0, 500);

    private static float StepPan(float pan, double delta) => (float)Math.Clamp(pan + delta, -100, 100);

    private static double StepOffset(double offset, double delta) => Math.Round(Math.Clamp(offset + delta, -60, 60), 4);
}

/// <summary>A panel that tunes something under the wheel; see <see cref="OnScroll" />.</summary>
public sealed class ScrollZone(UIContext context) : FlexPanel(context)
{
    /// <summary>Takes the wheel delta; returning false lets the wheel bubble on (to a scroll view).</summary>
    public Func<Vector2, bool>? OnScroll { get; set; }

    public override bool HandleScroll(Vector2 scrollDelta) => OnScroll?.Invoke(scrollDelta) ?? false;
}
