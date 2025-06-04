namespace BMS2TDW.Objects;

public struct BmsMeasure()
{
    public readonly List<(int channel, BmsEvent bms_event)> Events = [];
}