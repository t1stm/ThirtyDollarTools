using System.Xml;
using Sunder.Markup.Document.Root;

namespace Sunder.Markup.Document.Logic;

public class LogicContainer(RootContainer root, XmlElement logicElement)
{
    public RootContainer Root { get; } = root;
    public string SourceCode { get; } = logicElement.InnerText;
    public string Language { get; } = logicElement.GetAttribute("language");
    public List<string> LanguageImports { get; } = GetLanguageImports(logicElement.GetAttribute("imports"));

    private static List<string> GetLanguageImports(string imports)
    {
        List<string> importsList = [];
        if (imports.Length == 0) return importsList;
        
        if (imports.StartsWith('[') && imports.EndsWith(']'))
            importsList = imports[1..^1].Split(',').ToList();
        else importsList.Add(imports);
        return importsList;
    }
}