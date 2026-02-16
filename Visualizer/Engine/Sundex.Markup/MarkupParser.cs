using System.Xml;
using Sunder.Markup.Document;
using Sunder.Markup.Document.Root;

namespace Sunder.Markup;

public class MarkupParser
{
    public static SundexDocument Parse(string markup)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(markup);

        var rootElement = xmlDocument.DocumentElement;
        if (rootElement == null) throw new XmlException("The root element is missing.");
        var root = new RootContainer(rootElement);
        
        return new SundexDocument
        {
            Root = root,
        };
    }
}