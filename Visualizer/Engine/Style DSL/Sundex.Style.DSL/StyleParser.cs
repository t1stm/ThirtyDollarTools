using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL;

public class StyleParser(
    string dsl,
    string? basePath = null,
    Func<string, string>? fileLoader = null,
    HashSet<string>? importedPaths = null)
{
    private int _pos;
    private readonly HashSet<string> _importedPaths = importedPaths ?? [];

    public static StyleSheet Parse(string dsl, string? basePath = null, Func<string, string>? fileLoader = null)
    {
        var parser = new StyleParser(dsl, basePath, fileLoader);
        return parser.ParseSheet();
    }

    private StyleSheet ParseSheet()
    {
        var sheet = new StyleSheet();
        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            if (Match("animation")) ParseBlock(sheet.Animations, false, false, sheet);
            else if (Match("component")) ParseBlock(sheet.Components, true, false, sheet);
            else if (Match("class")) ParseBlock(sheet.Classes, false, false, sheet);
            else if (Match("id")) ParseBlock(sheet.IDTags, false, false, sheet);
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
        bool isOverride = false, StyleSheet? sheet = null)
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
        if (identifier == "vec2") return ParseVector();

        return new StringValue(identifier); // Fallback for unquoted strings/keywords
    }

    private NumberValue ParseNumber()
    {
        var start = _pos;
        if (Peek() == '-') Advance();
        while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.')) Advance();
        var numStr = dsl[start.._pos];
        var val = double.Parse(numStr);

        var unitStart = _pos;
        while (!IsAtEnd() && char.IsLetter(Peek()) || Peek() == '%') Advance();
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
            if (Check(';')) Advance();
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

    private VectorValue ParseVector()
    {
        Consume('(');
        var x = ParseValue();
        SkipWhitespaceAndComments();
        Consume(',');
        var y = ParseValue();
        SkipWhitespaceAndComments();
        Consume(')');
        return new VectorValue(((NumberValue)x).Value, ((NumberValue)y).Value);
    }

    private void ParseImport(StyleSheet sheet)
    {
        SkipWhitespaceAndComments();
        var path = ReadString();
        SkipWhitespaceAndComments();
        if (Check(';')) Advance();

        if (fileLoader == null) return;

        var fullPath = basePath != null ? Path.Combine(basePath, path) : path;
        try
        {
            fullPath = Path.GetFullPath(fullPath);
        }
        catch
        {
            // Fallback for non-file paths in tests if needed
        }

        if (!_importedPaths.Add(fullPath)) return;

        var importedDsl = fileLoader(fullPath);
        var importedParser = new StyleParser(importedDsl, Path.GetDirectoryName(fullPath), fileLoader, _importedPaths);
        var importedSheet = importedParser.ParseSheet();
        sheet.Merge(importedSheet);
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
        if (Peek() != c) throw new Exception($"Expected {c} but found {Peek()} at {_pos}");
        Advance();
    }
}