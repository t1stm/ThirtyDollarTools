namespace BMS2TDW.Converter;

public readonly struct CombineStack()
{
    public readonly List<string> Events = ["_pause|"];

    public string Export()
    {
        return string.Join("!combine|", Events.Where(r => !string.IsNullOrWhiteSpace(r))) + '\n';
    }
}