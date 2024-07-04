using BMS2TDW.Objects;

namespace BMS2TDW;

public struct BMSLevel()
{
    public BMSHeader Header = new();
    public BMSData Data = new();
}