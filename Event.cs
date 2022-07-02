namespace ThirtyDollarWebsiteConverter
{
    public class Event
    {
        public SoundEvent? SoundEvent { get; init; }
        public int SampleId => (int?) SoundEvent ?? 0;
        public int SampleLength => SampleId > Program.Samples.Count ? 0 : Program.Samples[SampleId].Length;
        public double Value { get; set; }
        public int Loop { get; set; } = 1;
        public double Volume { get; set; } = 100;
        public ValueScale ValueScale { get; init; }
        public override string ToString()
        {
            return $"Event: {SoundEvent}, Value: {Value}{(ValueScale == ValueScale.Times ? 'x' : (char) 0)}, Loops: {Loop}";
        }
    }

    public enum ValueScale
    {
        Times, 
        Add,
        None
    }
}