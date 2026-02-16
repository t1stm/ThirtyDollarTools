using System.Text.Json;
using System.Xml;
using Sunder.Markup.Document.Layout;
using Sunder.Markup.Document.Logic;
using Sunder.Markup.Document.Style;

namespace Sunder.Markup.Document.Root;

public class RootContainer
{
    public RootContainer(XmlElement rootElement)
    {
        if (rootElement is not { Name: "sunder" })
        {
            throw new XmlException("Root element must be <sunder>");
        }
        
        var layoutElement = rootElement["layout"];
        if (layoutElement == null)
        {
            throw new XmlException("The <layout> element is required in <sunder>.");
        }
        
        RootElement = rootElement;
        Layout = new LayoutContainer(this, layoutElement);
     
        var logicElement = rootElement["logic"];
        var styleElement = rootElement["style"];
        
        if (logicElement != null)
            Logic = new LogicContainer(this, logicElement);
        
        if (styleElement != null)
            Style = new StyleContainer(this, styleElement);
        
        Version = rootElement.GetAttribute("version");
        if (Version.Length == 0) 
            Version = "1.0";
        
        var component = rootElement.GetAttribute("component");
        if (component.Length > 0)
            Component = component;
        
        var implements = rootElement.GetAttribute("implements");
        if (implements.Length > 0)
            Implements = implements;
        
        Imports = TryParseListTypeAttribute(rootElement, "imports");
        Collections = TryParseListTypeAttribute(rootElement, "collections");
    }

    private static List<string> TryParseListTypeAttribute(XmlElement rootElement, string attribute)
    {
        var imports = rootElement.GetAttribute(attribute);

        if (imports.Length == 0) return [];
        if (!imports.StartsWith('[') || !imports.EndsWith(']')) 
            throw new XmlException($"The {attribute} attribute must be a JSON string array.");

        var array = JsonSerializer.Deserialize<List<string>>(imports);
        return array ?? throw new JsonException($"Failed to deserialize {attribute} attribute.");
    }
    
    public XmlElement RootElement { get; }
    public LayoutContainer Layout { get; }
    public LogicContainer? Logic { get; }
    public StyleContainer? Style { get; }
    
    public string Version { get; private set; }
    public string? Component { get; private set; }
    public string? Implements { get; private set; }
    
    public List<string> Collections { get; private set; } = [];
    public List<string> Imports { get; private set; } = [];
}