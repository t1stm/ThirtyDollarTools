namespace ThirtyDollarConverter.CLI;

public static class Progressbar
{
    private const char EmptyBlock = '□', FullBlock = '■';
    
    public static string Generate(double current, long total, int length = 32)
    {
        Span<char> prg = stackalloc char[length];

        var increment = total / length;
        var display = (int)Math.Floor(current / increment);
        display = display > length ? length : display;
        if (display < 0) display = 0;
        for (var i = 0; i < display; i++) prg[i] = FullBlock;

        for (var i = display; i < length; i++) prg[i] = EmptyBlock;

        return prg.ToString();
    }
}