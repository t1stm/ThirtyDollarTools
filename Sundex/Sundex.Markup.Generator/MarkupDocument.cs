using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Sundex.Markup.Generator;

/// <summary>A position in a markup file, 1-based, for diagnostics and <c>#line</c> directives.</summary>
internal readonly struct Spot(int line, int column)
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}

internal sealed class MarkupAttribute(string name, string value, Spot spot)
{
    public string Name { get; } = name;
    public string Value { get; } = value;
    public Spot Spot { get; } = spot;
}

internal sealed class MarkupNode(string tag, Spot spot)
{
    public string Tag { get; } = tag;
    public Spot Spot { get; } = spot;
    public List<MarkupAttribute> Attributes { get; } = [];
    public List<MarkupNode> Children { get; } = [];

    public MarkupAttribute? Attribute(string name)
    {
        foreach (var attribute in Attributes)
            if (attribute.Name == name)
                return attribute;
        return null;
    }
}

/// <summary>An <c>&lt;event name="pick" type="string"/&gt;</c>: what a use of the component can listen to with <c>on.pick</c>.</summary>
internal sealed class MarkupEvent(string name, string? type, Spot spot)
{
    public string Name { get; } = name;

    /// <summary>The argument's type, or null for an event with none.</summary>
    public string? Type { get; } = type;

    public Spot Spot { get; } = spot;
}

internal sealed class MarkupProp(string name, string type, string? defaultValue, Spot spot)
{
    public string Name { get; } = name;
    public string Type { get; } = type;
    public string? Default { get; } = defaultValue;
    public Spot Spot { get; } = spot;
}

/// <summary>One parsed <c>.snx.xml</c> file. Only version 2.0 documents are compiled; the rest are left to the runtime builder.</summary>
internal sealed class MarkupDocument
{
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Error { get; set; }
    public Spot ErrorSpot { get; set; }

    /// <summary>The tag other documents use it by, e.g. <c>track-row</c>.</summary>
    public string Tag { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string? Context { get; set; }
    public string? Namespace { get; set; }
    public string? StyleSource { get; set; }
    public Spot StyleSpot { get; set; }
    public bool InlineStyle { get; set; }
    public List<string> Imports { get; } = [];
    public string? Logic { get; set; }
    public Spot LogicSpot { get; set; }

    /// <summary>The <c>.snx.csx</c> file the logic is in (<c>&lt;logic src="…"/&gt;</c>), when it isn't inline.</summary>
    public string? LogicSource { get; set; }
    public Spot LogicSourceSpot { get; set; }
    public List<MarkupProp> Props { get; } = [];
    public List<MarkupEvent> Events { get; } = [];
    public MarkupNode? Root { get; set; }
    public Spot LayoutSpot { get; set; }
    public int LayoutRoots { get; set; }

    public static MarkupDocument Parse(string path, string text)
    {
        var document = new MarkupDocument { Path = path };
        var file = System.IO.Path.GetFileName(path);
        var stem = file.EndsWith(".snx.xml", StringComparison.OrdinalIgnoreCase) ? file.Substring(0, file.Length - 8) : file;

        try
        {
            using var reader = XmlReader.Create(new StringReader(text),
                new XmlReaderSettings { IgnoreComments = true, IgnoreProcessingInstructions = true, DtdProcessing = DtdProcessing.Ignore });
            var info = (IXmlLineInfo)reader;
            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "sundex")
                throw new MarkupException("The root element must be <sundex>.", Here(info));

            document.Version = reader.GetAttribute("version") ?? "";
            if (document.Version != "2.0") return document;

            var component = reader.GetAttribute("component");
            document.Tag = string.IsNullOrEmpty(component) ? Kebab(stem) : component!;
            document.ClassName = Pascal(document.Tag);
            document.Context = reader.GetAttribute("context");
            document.Namespace = reader.GetAttribute("namespace");

            if (reader.IsEmptyElement) return document;
            reader.Read();
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "sundex") break;
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                var spot = Here(info);
                switch (reader.Name)
                {
                    case "style":
                        document.StyleSource = reader.GetAttribute("src");
                        document.StyleSpot = spot;
                        document.InlineStyle = ReadText(reader).Trim().Length > 0;
                        break;
                    case "logic":
                        foreach (var import in List(reader.GetAttribute("imports"))) document.Imports.Add(import);
                        document.LogicSource = reader.GetAttribute("src");
                        document.LogicSourceSpot = spot;
                        document.LogicSpot = spot;
                        document.Logic = ReadLogic(reader, info, out var logicSpot);
                        document.LogicSpot = logicSpot;
                        break;
                    case "props":
                        ReadProps(reader, info, document);
                        break;
                    case "layout":
                        document.LayoutSpot = spot;
                        ReadLayout(reader, info, document);
                        break;
                    default:
                        throw new MarkupException($"<{reader.Name}> can't be a direct child of <sundex>.", spot);
                }
            }
        }
        catch (MarkupException exception)
        {
            document.Error = exception.Message;
            document.ErrorSpot = exception.Spot;
        }
        catch (XmlException exception)
        {
            document.Error = exception.Message;
            document.ErrorSpot = new Spot(exception.LineNumber, exception.LinePosition);
        }

        return document;
    }

    private static Spot Here(IXmlLineInfo info) => new(info.LineNumber, info.LinePosition);

    private static string ReadText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return "";
        }

        var text = reader.ReadElementContentAsString();
        return text;
    }

    /// <summary>Reads a logic block, noting where its code starts so <c>#line</c> lands on the right line.</summary>
    private static string ReadLogic(XmlReader reader, IXmlLineInfo info, out Spot start)
    {
        start = Here(info);
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return "";
        }

        var builder = new StringBuilder();
        var first = true;
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "logic"))
        {
            if (reader.NodeType is XmlNodeType.CDATA or XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                if (first)
                {
                    // The code is emitted from its first character on, whitespace included, so the
                    // #line goes where that character is.
                    start = Here(info);
                    first = false;
                }

                builder.Append(reader.Value);
            }

            if (!reader.Read()) break;
        }

        reader.Read();
        return builder.ToString();
    }

    private static void ReadProps(XmlReader reader, IXmlLineInfo info, MarkupDocument document)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "props"))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var spot = Here(info);
                if (reader.Name == "event")
                {
                    var eventName = reader.GetAttribute("name");
                    if (string.IsNullOrEmpty(eventName)) throw new MarkupException("An <event> needs a name.", spot);
                    var eventType = reader.GetAttribute("type");
                    document.Events.Add(new MarkupEvent(eventName!, string.IsNullOrEmpty(eventType) ? null : eventType, spot));
                    if (!reader.Read()) break;
                    continue;
                }

                if (reader.Name != "prop") throw new MarkupException($"<props> holds <prop> and <event> elements, not <{reader.Name}>.", spot);
                var name = reader.GetAttribute("name");
                var type = reader.GetAttribute("type");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
                    throw new MarkupException("A <prop> needs a name and a type.", spot);
                document.Props.Add(new MarkupProp(name!, type!, reader.GetAttribute("default"), spot));
            }

            if (!reader.Read()) break;
        }

        reader.Read();
    }

    private static void ReadLayout(XmlReader reader, IXmlLineInfo info, MarkupDocument document)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Name == "layout"))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var node = ReadNode(reader, info);
                document.LayoutRoots++;
                document.Root ??= node;
                continue;
            }

            if (reader.NodeType == XmlNodeType.Text && reader.Value.Trim().Length > 0)
                throw new MarkupException("Text in a layout belongs in an attribute, e.g. <label value=\"…\"/>.", Here(info));
            if (!reader.Read()) break;
        }

        reader.Read();
    }

    /// <summary>Reads the element the reader is on, and its subtree, leaving the reader after it.</summary>
    private static MarkupNode ReadNode(XmlReader reader, IXmlLineInfo info)
    {
        var node = new MarkupNode(reader.Name, Here(info));
        var empty = reader.IsEmptyElement;
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                node.Attributes.Add(new MarkupAttribute(reader.Name, reader.Value, Here(info)));
            } while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        reader.Read();
        if (empty) return node;

        while (!(reader.NodeType == XmlNodeType.EndElement))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                node.Children.Add(ReadNode(reader, info));
                continue;
            }

            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA && reader.Value.Trim().Length > 0)
                throw new MarkupException($"Text inside <{node.Tag}> belongs in an attribute, e.g. value=\"…\".", Here(info));
            if (!reader.Read()) break;
        }

        reader.Read();
        return node;
    }

    private static IEnumerable<string> List(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        var text = value!.Trim();
        if (text.StartsWith("[") && text.EndsWith("]")) text = text.Substring(1, text.Length - 2);
        foreach (var part in text.Split(','))
            if (part.Trim().Length > 0)
                yield return part.Trim();
    }

    public static string Pascal(string name)
    {
        var builder = new StringBuilder();
        var upper = true;
        foreach (var character in name)
        {
            if (character is '-' or '_' or '.' or ' ')
            {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(character) : character);
            upper = false;
        }

        return builder.ToString();
    }

    public static string Kebab(string name)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsUpper(character) && index > 0 && name[index - 1] != '-') builder.Append('-');
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}

internal sealed class MarkupException(string message, Spot spot) : Exception(message)
{
    public Spot Spot { get; } = spot;
}
