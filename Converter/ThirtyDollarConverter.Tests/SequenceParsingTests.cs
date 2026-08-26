using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Tests;

public class SequenceParsingTests
{
    /// <summary>
    ///     "!stop" counts down <see cref="BaseEvent.WorkingValue" />, so an event that reaches
    ///     the calculator with it at zero silently does nothing. Every event of a "#define" is
    ///     widened to an ExtendedEvent as the define is expanded, and that copy used to leave
    ///     WorkingValue behind - which made "!stop" (and "!loopmany") a no-op inside a define,
    ///     and only inside one.
    /// </summary>
    [Fact]
    public void Stop_InsideADefine_StillWaits()
    {
        const string defined = "#define(gap)|!stop@4|#enddefine|kick|gap|kick";
        const string inline = "kick|!stop@4|kick";

        Assert.Equal(Positions(inline), Positions(defined));
    }

    [Fact]
    public void Loopmany_InsideADefine_StillLoops()
    {
        const string defined = "#define(again)|!loopmany@2|#enddefine|!looptarget|kick|again";
        const string inline = "!looptarget|kick|!loopmany@2";

        Assert.Equal(Positions(inline), Positions(defined));
    }

    /// <summary>Where every sound of a sequence lands, in samples.</summary>
    private static List<ulong> Positions(string text)
    {
        return
        [
            .. new PlacementCalculator(new Objects.EncoderSettings { SampleRate = 48000 })
                .CalculateOne(Sequence.FromString(text))
                .Where(p => p.Audible && !(p.Event.SoundEvent?.StartsWith('!') ?? true))
                .Select(p => p.Index)
        ];
    }

    [Fact]
    public void LegacyMarker_SetsIsNewFormatFalse()
    {
        var seq = Sequence.FromString("#legacy|sound@1");
        Assert.False(seq.IsNewFormat);
    }

    [Fact]
    public void LegacyMarker_NotAddedToSequenceEvents()
    {
        var seq = Sequence.FromString("#legacy|sound@1");
        Assert.DoesNotContain(seq.Events, e => e is LegacySequenceEvent);
    }

    [Fact]
    public void DirectEvent_InLegacySequence_ConvertsPanToNewFormat()
    {
        var seq = Sequence.FromString("#legacy|sound@1^-0.8");
        var extendedEvent = seq.Events.OfType<ExtendedEvent>().Single();
        Assert.Equal(-80f, extendedEvent.Pan, 0.01f);
    }

    [Fact]
    public void DirectEvent_InLegacySequence_PositivePan_ConvertsPanToNewFormat()
    {
        var seq = Sequence.FromString("#legacy|sound@1^0.6");
        var extendedEvent = seq.Events.OfType<ExtendedEvent>().Single();
        Assert.Equal(60f, extendedEvent.Pan, 0.01f);
    }

    [Fact]
    public void DefinedEvent_InLegacySequence_ConvertsPanToNewFormat()
    {
        const string sequence = "#legacy|#define(snr)|foo@1^-0.8|#enddefine|snr";
        var seq = Sequence.FromString(sequence);
        var extendedEvent = seq.Events.OfType<ExtendedEvent>().Single();
        Assert.Equal(-80f, extendedEvent.Pan, 0.01f);
    }

    [Fact]
    public void DefinedEvent_InLegacySequence_ZeroPan_StaysZero()
    {
        const string sequence = "#legacy|#define(kick)|foo@1|#enddefine|kick";
        var seq = Sequence.FromString(sequence);
        var normalEvent = seq.Events.OfType<NormalEvent>().Single();
        Assert.Equal("foo", normalEvent.SoundEvent);
    }

    [Fact]
    public void NonLegacySequence_DirectEvent_DoesNotConvertPan()
    {
        var seq = Sequence.FromString("sound@1^-0.8");
        var extendedEvent = seq.Events.OfType<ExtendedEvent>().Single();
        Assert.Equal(-0.8f, extendedEvent.Pan, 0.0001f);
    }

    [Fact]
    public void NonLegacySequence_IsNewFormatTrue()
    {
        var seq = Sequence.FromString("sound@1");
        Assert.True(seq.IsNewFormat);
    }
}