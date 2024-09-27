namespace BMS2TDW.Objects;

public struct BMSMeasure()
{
    public readonly List<(int channel, BMSEvent bms_event)> Events = [];
}