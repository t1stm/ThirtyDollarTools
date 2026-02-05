namespace ThirtyDollarVisualizer.Engine.Text.Allocationless;

public class StringFormatter
{
    private char[] _output = [];
    private int _totalCurrentLength;
    
    private readonly Dictionary<string, long> _fixedNumbers = [];
    private readonly Dictionary<string, double> _floatingNumbers = [];
    private readonly Dictionary<string, TimeSpan> _times = [];
    private readonly Dictionary<string, char[]> _strings = [];
    private readonly Dictionary<string, bool> _booleans = [];
    private readonly Dictionary<string, string> _formats = [];
    private readonly Dictionary<string, Segment> _parameterSegments = [];
    private readonly List<Segment> _segments = [];
    
    private class Segment
    {
        public bool IsParameter;
        public string Name; // Parameter name or Literal content
        public int Start;
        public int MaxLength;
        public int CurrentLength;
    }
    
    public StringFormatter(string format)
    {
        ParseFormat(format);
    }

    public ReadOnlySpan<char> Value => _output.AsSpan(0, _totalCurrentLength);

    public void Set(ReadOnlySpan<char> name, long value)
    {
        var lookup = _fixedNumbers.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup[name] = value;
        UpdateOutput(name, value);
    }

    public void Set(ReadOnlySpan<char> name, double value)
    {
        var lookup = _floatingNumbers.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup[name] = value;
        UpdateOutput(name, value);
    }

    public void Set(ReadOnlySpan<char> name, TimeSpan value)
    {
        var lookup = _times.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup[name] = value;
        UpdateOutput(name, value);
    }

    public void Set(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
    {
        var lookup = _strings.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(name, out var buffer))
        {
            var length = Math.Min(value.Length, buffer.Length);
            value[..length].CopyTo(buffer);
        }
        UpdateOutput(name, value);
    }

    public void Set(ReadOnlySpan<char> name, bool value)
    {
        var lookup = _booleans.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup[name] = value;
        UpdateOutput(name, value ? "True" : "False");
    }

    private void ApplyUpdate(Segment segment, ReadOnlySpan<char> newValue)
    {
        int delta = newValue.Length - segment.CurrentLength;
        if (delta != 0)
        {
            int restStart = segment.Start + segment.CurrentLength;
            int restCount = _totalCurrentLength - restStart;
            
            if (restCount > 0)
            {
                _output.AsSpan(restStart, restCount).CopyTo(_output.AsSpan(restStart + delta));
            }

            // Update starts of following segments
            bool found = false;
            foreach (var s in _segments)
            {
                if (found) s.Start += delta;
                else if (s == segment) found = true;
            }
            
            _totalCurrentLength += delta;
        }

        newValue.CopyTo(_output.AsSpan(segment.Start));
        segment.CurrentLength = newValue.Length;
    }

    private void UpdateOutput<T>(ReadOnlySpan<char> name, T value) where T : ISpanFormattable
    {
        var lookup = _parameterSegments.GetAlternateLookup<ReadOnlySpan<char>>();
        if (!lookup.TryGetValue(name, out var segment)) return;

        var formatsLookup = _formats.GetAlternateLookup<ReadOnlySpan<char>>();
        var format = formatsLookup.TryGetValue(name, out var f) ? f.AsSpan() : default;

        Span<char> scratch = stackalloc char[256];
        if (value.TryFormat(scratch, out int bytesWritten, format, null))
        {
            ApplyUpdate(segment, scratch[..Math.Min(bytesWritten, segment.MaxLength)]);
        }
    }

    private void UpdateOutput(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
    {
        var lookup = _parameterSegments.GetAlternateLookup<ReadOnlySpan<char>>();
        if (!lookup.TryGetValue(name, out var segment)) return;

        ApplyUpdate(segment, value[..Math.Min(value.Length, segment.MaxLength)]);
    }

    private void ParseFormat(string format)
    {
        var span = format.AsSpan();
        var totalMaxLength = 0;
        int segmentsCount = 0;

        // Pass 1: Calculate total max length and count segments
        var i = 0;
        while (i < span.Length)
        {
            var openBrace = span[i..].IndexOf('{');
            if (openBrace == -1)
            {
                totalMaxLength += span.Length - i;
                segmentsCount++;
                break;
            }

            if (openBrace > 0)
            {
                totalMaxLength += openBrace;
                segmentsCount++;
            }
            
            i += openBrace;

            var closeBrace = span[i..].IndexOf('}');
            if (closeBrace == -1)
            {
                totalMaxLength += span.Length - i;
                segmentsCount++;
                break;
            }

            var tagContent = span[(i + 1)..(i + closeBrace)];
            totalMaxLength += GetTagMaxLength(tagContent);
            segmentsCount++;

            i += closeBrace + 1;
        }

        _output = new char[totalMaxLength];
        _segments.Clear();
        _segments.EnsureCapacity(segmentsCount);
        _parameterSegments.Clear();
        _totalCurrentLength = 0;

        // Pass 2: Create segments and fill _output with literals
        i = 0;
        while (i < span.Length)
        {
            var openBrace = span[i..].IndexOf('{');
            if (openBrace == -1)
            {
                AddLiteralSegment(span[i..]);
                break;
            }

            if (openBrace > 0)
            {
                AddLiteralSegment(span[i..(i + openBrace)]);
            }

            i += openBrace;

            var closeBrace = span[i..].IndexOf('}');
            var tagContent = span[(i + 1)..(i + closeBrace)];
            
            ProcessTag(tagContent);

            i += closeBrace + 1;
        }
    }

    private void AddLiteralSegment(ReadOnlySpan<char> content)
    {
        var seg = new Segment
        {
            IsParameter = false,
            Name = content.ToString(),
            Start = _totalCurrentLength,
            CurrentLength = content.Length,
            MaxLength = content.Length
        };
        _segments.Add(seg);
        content.CopyTo(_output.AsSpan(_totalCurrentLength));
        _totalCurrentLength += content.Length;
    }

    private void ProcessTag(ReadOnlySpan<char> content)
    {
        ParseTagParts(content, out var name, out var type, out var p3, out var p4);
        if (type.IsEmpty) return;
        
        var maxLength = GetDefaultMaxLength(type);
        ReadOnlySpan<char> format = default;

        if (p4.Length > 0)
        {
            format = p3;
            if (int.TryParse(p4, out var m)) maxLength = m;
        }
        else if (p3.Length > 0)
        {
            if (int.TryParse(p3, out var m))
            {
                maxLength = m;
            }
            else
            {
                format = p3;
            }
        }
        
        var nameStr = name.ToString();
        if (type.Equals("int", StringComparison.OrdinalIgnoreCase)) _fixedNumbers.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(nameStr, 0);
        else if (type.Equals("float", StringComparison.OrdinalIgnoreCase)) _floatingNumbers.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(nameStr, 0);
        else if (type.Equals("time", StringComparison.OrdinalIgnoreCase)) _times.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(nameStr, TimeSpan.Zero);
        else if (type.Equals("string", StringComparison.OrdinalIgnoreCase)) _strings.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(nameStr, new char[maxLength]);
        else if (type.Equals("bool", StringComparison.OrdinalIgnoreCase)) _booleans.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(nameStr, false);

        if (format.Length > 0)
        {
            _formats.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(nameStr, format.ToString());
        }

        var seg = new Segment
        {
            IsParameter = true,
            Name = nameStr,
            Start = _totalCurrentLength,
            CurrentLength = 0,
            MaxLength = maxLength
        };
        _segments.Add(seg);
        _parameterSegments[nameStr] = seg;
        // Parameters start empty, so _totalCurrentLength doesn't increase
    }

    private void ParseTagParts(ReadOnlySpan<char> content, out ReadOnlySpan<char> name, out ReadOnlySpan<char> type,
        out ReadOnlySpan<char> p3, out ReadOnlySpan<char> p4)
    {
        p3 = default;
        p4 = default;
        var firstColon = content.IndexOf(':');
        if (firstColon == -1)
        {
            name = content;
            type = default;
            return;
        }

        name = content[..firstColon];
        var typeAndMore = content[(firstColon + 1)..];
        var secondColon = typeAndMore.IndexOf(':');

        if (secondColon == -1)
        {
            type = typeAndMore;
            return;
        }

        type = typeAndMore[..secondColon];
        var remainder = typeAndMore[(secondColon + 1)..];
        
        var lastColon = remainder.LastIndexOf(':');
        if (lastColon != -1 && int.TryParse(remainder[(lastColon + 1)..], out _))
        {
            p3 = remainder[..lastColon];
            p4 = remainder[(lastColon + 1)..];
        }
        else
        {
            p3 = remainder;
        }
    }

    private int GetTagMaxLength(ReadOnlySpan<char> content)
    {
        ParseTagParts(content, out _, out var type, out var p3, out var p4);
        if (type.IsEmpty) return 0;

        var maxLength = GetDefaultMaxLength(type);
        if (p4.Length > 0)
        {
            if (int.TryParse(p4, out var m)) maxLength = m;
        }
        else if (p3.Length > 0)
        {
            if (int.TryParse(p3, out var m)) maxLength = m;
        }

        return maxLength;
    }

    private static int GetDefaultMaxLength(ReadOnlySpan<char> type)
    {
        if (type.Equals("int", StringComparison.OrdinalIgnoreCase)) return 20;
        if (type.Equals("float", StringComparison.OrdinalIgnoreCase) || type.Equals("time", StringComparison.OrdinalIgnoreCase)) return 32;
        if (type.Equals("string", StringComparison.OrdinalIgnoreCase)) return 256;
        return type.Equals("bool", StringComparison.OrdinalIgnoreCase) ? 5 : 0;
    }
}
