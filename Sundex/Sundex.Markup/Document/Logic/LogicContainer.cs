using System.Xml;
using Sundex.Markup.Document.Root;

namespace Sundex.Markup.Document.Logic;

public class LogicContainer(RootContainer root, XmlElement logicElement)
{
    public RootContainer Root { get; } = root;

    public string SourceCode { get; private set; } = logicElement.InnerText;
    public string SrcLocation { get; private set; } = logicElement.GetAttribute("src");

    public string Language { get; } = logicElement.GetAttribute("language");
    public List<string> LanguageImports { get; } = GetLanguageImports(logicElement.GetAttribute("imports"));

    private static List<string> GetLanguageImports(string imports)
    {
        List<string> importsList = [];
        if (imports.Length == 0) return importsList;

        if (imports.StartsWith('[') && imports.EndsWith(']'))
            importsList = [.. imports[1..^1].Split(',').Select(r => r.Trim())];
        else importsList.Add(imports);
        return importsList;
    }

    public void UpdateSourceCode(string value)
    {
        SourceCode = value;
    }
}