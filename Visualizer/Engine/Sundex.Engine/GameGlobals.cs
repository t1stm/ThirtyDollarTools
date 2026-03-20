namespace Sundex.Engine;

public class GameGlobals
{
    private readonly Dictionary<string, object?> _globals = new();

    public void Set<T>(ReadOnlySpan<char> key, T value)
    {
        var lookup = _globals.GetAlternateLookup<ReadOnlySpan<char>>();
        if (!lookup.TryAdd(key, value))
            throw new Exception($"Global variable '{key}' already exists");
    }

    public T Get<T>(ReadOnlySpan<char> key)
    {
        var lookup = _globals.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(key, out var value))
            return value is T castedValue
                ? castedValue
                : throw new InvalidCastException($"Global variable '{key}' is not of type {typeof(T)}");

        throw new InvalidDataException($"Global variable '{key}' not found");
    }
}