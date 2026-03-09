using System.Buffers;
using System.Text;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL;

public class StyleParser(
    string dsl,
    Func<string, string>? fileLoader = null,
    HashSet<string>? importedPaths = null)
{
    private int _pos;
    private readonly HashSet<string> _importedPaths = importedPaths ?? [];

    public static StyleSheetHolder Parse(string dsl, Func<string, string>? fileLoader = null)
    {
        var parser = new StyleParser(dsl, fileLoader);
        return parser.ParseSheet();
    }

    private StyleSheetHolder ParseSheet()
    {
        var sheet = new StyleSheetHolder();
        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            if (Match("animation")) ParseBlock(sheet.Animations, false, false, sheet);
            else if (Match("component")) ParseBlock(sheet.Components, true, false, sheet);
            else if (Match("class")) ParseBlock(sheet.Classes, true, false, sheet);
            else if (Match("id")) ParseBlock(sheet.IDTags, true, false, sheet);
            else if (Match("import")) ParseImport(sheet);
            else if (Peek() == '@')
            {
                Advance();
                if (Match("component")) ParseBlock(sheet.Components, true, true, sheet);
                else throw new Exception($"Unexpected token @ at {_pos}");
            }
            else throw new Exception($"Unexpected token {Peek()} at {_pos}");
        }

        return sheet;
    }

    private void ParseBlock(Dictionary<string, Dictionary<string, IStyleValue>> target, bool allowState = false,
        bool isOverride = false, StyleSheetHolder? sheet = null)
    {
        SkipWhitespaceAndComments();
        var name = ReadIdentifier();
        if (isOverride) sheet?.FullOverrides.Add(name);
        SkipWhitespaceAndComments();
        Consume('{');
        var properties = new Dictionary<string, IStyleValue>();
        while (!Check('}'))
        {
            SkipWhitespaceAndComments();
            if (Check('}')) break;

            if (allowState && Match("state"))
            {
                Consume('[');
                var stateName = ReadIdentifier();
                Consume(']');
                SkipWhitespaceAndComments();
                Consume('=');
                var stateValue = ParseValue();
                properties[$"state[{stateName}]"] = stateValue;
            }
            else
            {
                var key = ReadIdentifier();
                SkipWhitespaceAndComments();
                Consume('=');
                var value = ParseValue();
                properties[key] = value;
            }

            SkipWhitespaceAndComments();
            if (Check(';')) Advance();
            SkipWhitespaceAndComments();
        }

        Consume('}');

        if (isOverride)
        {
            target[name] = properties;
        }
        else if (target.TryGetValue(name, out var existingProperties))
        {
            foreach (var kvp in properties)
            {
                existingProperties[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            target[name] = properties;
        }
    }

    private IStyleValue ParseValue()
    {
        SkipWhitespaceAndComments();
        if (Check('"'))
        {
            var s = ReadString();
            if (s.StartsWith('#')) return new ColorValue(s);
            return new StringValue(s);
        }

        if (char.IsDigit(Peek()) || Peek() == '-') return ParseNumber();
        if (Peek() == '#') return new ColorValue(ReadHexColor());
        if (Peek() == '!') return ParseKeyword();
        if (Check('{')) return ParseNestedBlock();
        if (Check('[')) return ParseArrayOrMap();

        var identifier = ReadIdentifier();
        return identifier switch
        {
            "vec2" => ParseVector(2),
            "vec3" => ParseVector(3),
            "vec4" => ParseVector(4),
            _ => new StringValue(identifier)
        };
    }

    private NumberValue ParseNumber()
    {
        var start = _pos;
        if (Peek() == '-') Advance();
        while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.')) Advance();
        var numStr = dsl[start.._pos];
        var val = float.Parse(numStr);

        var unitStart = _pos;
        while (!IsAtEnd() && (char.IsLetter(Peek()) || Peek() == '%')) Advance();
        var unit = dsl[unitStart.._pos];

        return new NumberValue(val, unit);
    }

    private string ReadHexColor()
    {
        var start = _pos;
        Consume('#');
        while (!IsAtEnd() &&
               (char.IsDigit(Peek()) || (char.ToLower(Peek()) >= 'a' && char.ToLower(Peek()) <= 'f'))) Advance();
        return dsl[start.._pos];
    }

    private IStyleValue ParseKeyword()
    {
        Consume('!');
        var name = ReadIdentifier();
        SkipWhitespaceAndComments();
        return name switch
        {
            "override" => new OverrideValue(ParseValue()),
            "gradient" => new GradientValue(ParseValue()),
            "keyframes" => new KeyframesValue(ParseValue()),
            "stops" => new StopsValue(ParseValue()),
            "direction" => new DirectionValue(ParseValue()),
            _ => new KeywordValue(name)
        };
    }

    private BlockValue ParseNestedBlock()
    {
        Consume('{');
        var properties = new Dictionary<string, IStyleValue>();
        while (!Check('}'))
        {
            SkipWhitespaceAndComments();
            if (Check('}')) break;
            var key = ReadIdentifier();
            SkipWhitespaceAndComments();
            Consume('=');
            var value = ParseValue();
            properties[key] = value;
            SkipWhitespaceAndComments();
            // Allow either ';' or ',' as a property separator inside nested blocks
            if (Check(';') || Check(',')) Advance();
            SkipWhitespaceAndComments();
        }

        Consume('}');
        return new BlockValue(properties);
    }

    private IStyleValue ParseArrayOrMap()
    {
        Consume('[');
        var list = new List<IStyleValue>();
        var map = new Dictionary<IStyleValue, IStyleValue>();
        var isMap = false;

        while (!Check(']'))
        {
            SkipWhitespaceAndComments();
            if (Check(']')) break;
            var val = ParseValue();
            SkipWhitespaceAndComments();
            if (Check('='))
            {
                isMap = true;
                Advance();
                var val2 = ParseValue();
                map[val] = val2;
            }
            else
            {
                list.Add(val);
            }

            SkipWhitespaceAndComments();
            if (Check(',')) Advance();
            SkipWhitespaceAndComments();
        }

        Consume(']');
        return isMap ? new MapValue(map) : new ArrayValue(list);
    }

    private VectorValue ParseVector(int dimensions)
    {
        var values = ArrayPool<NumberValue>.Shared.Rent(dimensions);
        var span = values.AsSpan()[..dimensions];

        Consume('(');
        for (var i = 0; i < dimensions; i++)
        {
            SkipWhitespaceAndComments();
            var parsed = ParseValue();
            if (parsed is not NumberValue number)
                throw new Exception($"Expected number value for vector dimension {i + 1} but found {parsed}");

            span[i] = number;
            SkipWhitespaceAndComments();
            if (i < dimensions - 1)
                Consume(',');
        }

        Consume(')');

        ArrayPool<NumberValue>.Shared.Return(values);
        return new VectorValue(span);
    }

    private void ParseImport(StyleSheetHolder sheetHolder)
    {
        SkipWhitespaceAndComments();
        var path = ReadString();
        SkipWhitespaceAndComments();
        if (Check(';')) Advance();

        if (fileLoader == null) return;

        if (!_importedPaths.Add(path)) return;
        var importedDsl = fileLoader(path);

        var importedParser = new StyleParser(importedDsl, fileLoader, _importedPaths);
        var importedSheet = importedParser.ParseSheet();
        sheetHolder.Merge(importedSheet);
    }

    private string ReadIdentifier()
    {
        var start = _pos;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '-')) Advance();
        return dsl[start.._pos];
    }

    private string ReadString()
    {
        Consume('"');
        var start = _pos;
        while (!IsAtEnd() && Peek() != '"') Advance();
        var s = dsl[start.._pos];
        Consume('"');
        return s;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            if (char.IsWhiteSpace(Peek())) Advance();
            else if (Peek() == '/' && PeekNext() == '/')
            {
                while (!IsAtEnd() && Peek() != '\n') Advance();
            }
            else break;
        }
    }

    private bool Match(ReadOnlySpan<char> s)
    {
        if (_pos + s.Length > dsl.Length) return false;
        if (!dsl.AsSpan(_pos, s.Length).SequenceEqual(s)) return false;

        // Ensure word boundary
        if (_pos + s.Length < dsl.Length && (char.IsLetterOrDigit(dsl[_pos + s.Length]) || dsl[_pos + s.Length] == '-'))
        {
            return false;
        }

        _pos += s.Length;
        return true;
    }

    private bool Check(char c) => !IsAtEnd() && Peek() == c;
    private char Peek() => dsl[_pos];
    private char PeekNext() => _pos + 1 < dsl.Length ? dsl[_pos + 1] : '\0';
    private void Advance() => _pos++;
    private bool IsAtEnd() => _pos >= dsl.Length;

    private void Consume(char c)
    {
        var foundChar = IsAtEnd() ? '\0' : Peek();
        if (foundChar == c)
        {
            Advance();
            return;
        }

        const int linesBefore = 5;
        const int linesAfter = 5;

        var errorPosition = _pos;
        var text = dsl.AsSpan();

        int startI;
        int endI;

        var count = 0;
        for (startI = errorPosition; startI >= 0; startI--)
        {
            if (text[startI] == '\n')
                count++;

            if (count == linesBefore)
                break;
        }

        for (endI = errorPosition; endI < dsl.Length; endI++)
        {
            if (text[endI] == '\n')
                count++;

            if (count == linesAfter)
                break;
        }

        var slice = text[startI..endI];
        var normalizedPosition = errorPosition - startI;
        var stringified = slice.ToString();

        stringified = stringified.Insert(normalizedPosition, "<--- HERE");
        throw new Exception($"Expected '{c}' but found character '{foundChar}.\n" +
                            "=== SOURCE CODE===\n\n" +
                            stringified);
    }
}