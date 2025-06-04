using BMS2TDW.Objects;

namespace BMS2TDW;

public struct BmsLevel()
{
    public BmsHeader Header = new();
    public BmsData Data = new();
}