using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Views;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class ScrollGestureTests
{
    [Theory]
    [InlineData(false, false, false, TuneField.Value, 1)]
    [InlineData(true, true, false, TuneField.Value, 0.1)]
    [InlineData(true, false, false, TuneField.Volume, 5)]
    [InlineData(false, true, false, TuneField.Pan, 10)]
    [InlineData(false, false, true, TuneField.Offset, 0.01)]
    // Alt wins over everything else held with it.
    [InlineData(true, false, true, TuneField.Offset, 0.01)]
    [InlineData(false, true, true, TuneField.Offset, 0.01)]
    [InlineData(true, true, true, TuneField.Offset, 0.01)]
    public void ModifiersPickTheFieldAndStep(bool ctrl, bool shift, bool alt, TuneField field, double step)
    {
        Assert.Equal((field, step), ScrollGesture.For(ctrl, shift, alt));
    }

    [Fact]
    public void Volume_StartsFromAHundred_AndClamps()
    {
        var sound = new InstrumentSound { Sound = "kick" };

        ScrollGesture.Apply(sound, TuneField.Volume, -5);
        Assert.Equal(95, sound.Volume);

        ScrollGesture.Apply(sound, TuneField.Volume, -1000);
        Assert.Equal(0, sound.Volume);
        ScrollGesture.Apply(sound, TuneField.Volume, 1000);
        Assert.Equal(500, sound.Volume);
    }

    [Fact]
    public void ValueAndPan_Clamp()
    {
        var instrument = new Instrument();

        ScrollGesture.Apply(instrument, TuneField.Value, 1000);
        ScrollGesture.Apply(instrument, TuneField.Pan, -1000);

        Assert.Equal(TrackEditorView.MaxValue, instrument.Value);
        Assert.Equal(-100, instrument.Pan);
    }

    [Fact]
    public void RepeatedFineSteps_DoNotGatherFloatError()
    {
        var sound = new InstrumentSound { Sound = "kick" };

        for (var i = 0; i < 3; i++) ScrollGesture.Apply(sound, TuneField.Value, 0.1);
        for (var i = 0; i < 3; i++) ScrollGesture.Apply(sound, TuneField.Offset, 0.01);

        Assert.Equal(0.3, sound.Value); // exact, not 0.30000000000000004
        Assert.Equal(0.03, sound.Offset);
    }
}
