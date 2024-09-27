namespace BMS2TDW.Converter;

public readonly struct CombineStack()
{
    public readonly List<string> Events = [];

    public string Export()
    {
        return string.Join("!combine|", Events.Where(r => !string.IsNullOrWhiteSpace(r) && r is not "_pause")) + '\n';
    }
}