using System.Buffers;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL;

public class StyleParser(
    string dsl,
    Func<string, string>? fileLoader = null,
    Dictionary<string, StyleSheetHolder>? parsedImports = null)
{
    /// <summary>Variables declared in this file, for duplicate detection. Imported ones aren't in here.</summary>
    private readonly HashSet<string> _localVariables = [];

    // Merge-once guards, per file: importing the same path twice into one sheet merges it once.
    // Deliberately NOT shared with child parsers — recursion is broken by the parse cache instead.
    private readonly HashSet<string> _mergedBlocks = [];
    private readonly HashSet<string> _mergedVariables = [];

    /// <summary>Every file parsed so far, keyed by import path. Shared with child parsers.</summary>
    private readonly Dictionary<string, StyleSheetHolder> _parsedImports = parsedImports ?? new();

    private int _pos;

    /// <summary>The sheet this parser writes into. One parser parses one file into one sheet.</summary>
    private StyleSheetHolder _sheet = null!;

    public static StyleSheetHolder Parse(string dsl, Func<string, string>? fileLoader = null)
    {
        var parser = new StyleParser(dsl, fileLoader);
        return parser.ParseSheet();
    }

    private StyleSheetHolder ParseSheet(StyleSheetHolder? target = null)
    {
        var sheet = _sheet = target ?? new StyleSheetHolder();
        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            if (Match("animation"))
            {
                ParseBlock(sheet.Animations);
            }
            else if (Match("component"))
            {
                ParseBlock(sheet.Components, true);
            }
            else if (Match("class"))
            {
                ParseBlock(sheet.Classes, true);
            }
            else if (Match("id"))
            {
                ParseBlock(sheet.IDTags, true);
            }
            else if (Match("import"))
            {
                ParseImport();
            }
            else if (Match("var"))
            {
                ParseVariable();
            }
            else if (Peek() == '@')
            {
                Advance();
                if (Match("component")) ParseBlock(sheet.Components, true, true);
                else throw CreateException($"Unexpected token @ at {_pos}");
            }
            else
            {
                throw CreateException($"Unexpected token {Peek()} at {_pos}");
            }
        }

        return sheet;
    }

    private void ParseBlock(Dictionary<string, Dictionary<string, IStyleValue>> target, bool allowState = false,
        bool isOverride = false)
    {
        SkipWhitespaceAndComments();
        var name = ReadIdentifier();
        if (isOverride) _sheet.FullOverrides.Add(name);
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
            target[name] = properties;
        else if (target.TryGetValue(name, out var existingProperties))
            foreach (var kvp in properties)
                existingProperties[kvp.Key] = kvp.Value;
        else
            target[name] = properties;
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
        if (Peek() == '$') return ParseVariableReference();
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

    private void ParseVariable()
    {
        SkipWhitespaceAndComments();
        var name = ReadIdentifier();
        SkipWhitespaceAndComments();
        Consume('=');
        var value = ParseValue();
        SkipWhitespaceAndComments();
        if (Check(';')) Advance();

        if (!_localVariables.Add(name))
            throw CreateException($"Variable '{name}' is already defined in this file");

        // Plain assignment, so a local declaration may shadow one that came from an import.
        _sheet.Variables[name] = value;
    }

    private IStyleValue ParseVariableReference()
    {
        Consume('$');
        var name = ReadIdentifier();

        if (Check('.'))
        {
            Advance();
            var member = ReadIdentifier();
            if (!_sheet.Namespaces.TryGetValue(name, out var imported))
                throw CreateException($"Unknown import alias '{name}'");
            if (!imported.TryGetValue(member, out var scoped))
                throw CreateException($"Import '{name}' has no variable '{member}'");
            return scoped;
        }

        if (!_sheet.Variables.TryGetValue(name, out var value))
            throw CreateException($"Unknown variable '{name}'");

        // ponytail: shared reference; values are treated as immutable post-parse.
        return value;
    }

    private NumberValue ParseNumber()
    {
        var start = _pos;
        if (Peek() == '-') Advance();
        while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.')) Advance();
        var numStr = dsl[start.._pos];
        if (!float.TryParse(numStr, out var val))
            throw CreateException($"Failed to parse number: {numStr}");

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
                throw CreateException($"Expected number value for vector dimension {i + 1} but found {parsed}");

            span[i] = number;
            SkipWhitespaceAndComments();
            if (i < dimensions - 1)
                Consume(',');
        }

        Consume(')');

        ArrayPool<NumberValue>.Shared.Return(values);
        return new VectorValue(span);
    }

    private void ParseImport()
    {
        SkipWhitespaceAndComments();
        var path = ReadString();
        SkipWhitespaceAndComments();

        string? alias = null;
        if (Match("as"))
        {
            SkipWhitespaceAndComments();
            alias = ReadIdentifier();
            SkipWhitespaceAndComments();
        }

        if (Check(';')) Advance();

        if (fileLoader == null) return;

        if (!_parsedImports.TryGetValue(path, out var imported))
        {
            // Registered before recursing, so a cycle sees the partial holder instead of looping.
            imported = new StyleSheetHolder();
            _parsedImports[path] = imported;

            var importedParser = new StyleParser(fileLoader(path), fileLoader, _parsedImports);
            importedParser.ParseSheet(imported);
        }

        // Blocks and variables are merged at most once per path, independently: an aliased import
        // followed by a plain one still brings the file's variables into the global scope.
        if (_mergedBlocks.Add(path))
            _sheet.Merge(imported, false);

        if (alias != null)
            _sheet.Namespaces[alias] = imported.Variables;
        else if (_mergedVariables.Add(path))
            foreach (var (name, value) in imported.Variables)
                _sheet.Variables[name] = value;
    }

    private string ReadIdentifier()
    {
        var start = _pos;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '-' || Peek() == '_')) Advance();
        return dsl[start.._pos];
    }

    private string ReadString()
    {
        Consume('"');
        var start = _pos;
        while (!IsAtEnd() && Peek() != '"') Advance();
        if (IsAtEnd()) throw CreateException("Unterminated string");
        var s = dsl[start.._pos];
        Consume('"');
        return s;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
            if (char.IsWhiteSpace(Peek())) Advance();
            else if (Peek() == '/' && PeekNext() == '/')
                while (!IsAtEnd() && Peek() != '\n')
                    Advance();
            else break;
    }

    private bool Match(ReadOnlySpan<char> s)
    {
        if (_pos + s.Length > dsl.Length) return false;
        if (!dsl.AsSpan(_pos, s.Length).SequenceEqual(s)) return false;

        // Ensure word boundary
        if (_pos + s.Length < dsl.Length &&
            (char.IsLetterOrDigit(dsl[_pos + s.Length]) || dsl[_pos + s.Length] is '-' or '_')) return false;

        _pos += s.Length;
        return true;
    }

    private bool Check(char c)
    {
        return !IsAtEnd() && Peek() == c;
    }

    private char Peek()
    {
        return dsl[_pos];
    }

    private char PeekNext()
    {
        return _pos + 1 < dsl.Length ? dsl[_pos + 1] : '\0';
    }

    private void Advance()
    {
        _pos++;
    }

    private bool IsAtEnd()
    {
        return _pos >= dsl.Length;
    }

    private Exception CreateException(string message)
    {
        const int linesBefore = 5;
        const int linesAfter = 5;

        var errorPosition = Math.Min(_pos, dsl.Length);
        var text = dsl.AsSpan();

        var startI = errorPosition;
        for (var count = 0; startI > 0 && count < linesBefore; startI--)
            if (text[startI - 1] == '\n')
                count++;

        var endI = errorPosition;
        for (var count = 0; endI < dsl.Length && count < linesAfter; endI++)
            if (text[endI] == '\n')
                count++;

        var slice = text[startI..endI];
        var normalizedPosition = errorPosition - startI;
        var stringified = slice.ToString();

        stringified = stringified.Insert(normalizedPosition, "<--- HERE");
        return new Exception(message + ".\n" +
                             "=== SOURCE CODE===\n\n" +
                             stringified);
    }

    private void Consume(char c)
    {
        var foundChar = IsAtEnd() ? '\0' : Peek();
        if (foundChar != c) throw CreateException($"Expected '{c}' but found character '{foundChar}'");
        Advance();
    }
}