namespace BMS2TDW.Objects;

public struct BMSEvent
{
    public int BeatsDivision; // 1/1, 1/4, 1/8 etc.
    public string StringValue;
    public string[]? SoundsArray;
}