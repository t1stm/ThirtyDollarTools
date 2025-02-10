namespace BMS2TDW.Objects;

public class BMSMeasure
{
    public readonly List<(int channel, BMSEvent bms_event)> Events = [];
    public double Length = 1;
    public double? BPM;
}