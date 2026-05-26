# Markup Parser

The "XML → in-memory document" stage. `MarkupParser.Parse(string)` is the single entry point; everything below is the data structure it produces.

> Source: `Sundex/Sundex.Markup/MarkupParser.cs`, `Sundex/Sundex.Markup/Document/`, `Sundex/Sundex.Markup/Layout/SundexNode.cs`.

## `MarkupParser` — five lines of code

```csharp
public class MarkupParser
{
    public static SundexDocument Parse(string markup)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(markup);
        var rootElement = xmlDocument.DocumentElement;
        if (rootElement == null) throw new XmlException("The root element is missing.");
        var root = new RootContainer(rootElement);
        return new SundexDocument { Root = root };
    }
}
```

Hands the XML to `System.Xml.XmlDocument`, hands the root `XmlElement` to `RootContainer`, hands that to `SundexDocument`. All the work happens in the constructors of those classes — `MarkupParser` is just the public surface.

The `SundexDocument` returned is the in-memory representation; nothing has been "realised" yet (no `UIElement`s exist, no scripts compiled, no stylesheets parsed). That's what [[Phases/Component Builders|`ComponentBuilderV1`]] does later.

## `SundexDocument`

```csharp
public class SundexDocument
{
    public required RootContainer Root { get; init; }
    public LayoutContainer Layout => Root.Layout;
    public LogicContainer? Logic => Root.Logic;
    public StyleContainer? Style => Root.Style;
}
```

Pure pass-through. The class exists as a stable seam between parsing and building — `MarkupParser` produces it, `IComponentBuilder.CreateComponent(SundexDocument, ...)` consumes it.

## `RootContainer` — the `<sundex>` element

```csharp
public class RootContainer
{
    public RootContainer(XmlElement rootElement)
    {
        if (rootElement is not { Name: "sundex" })
            throw new XmlException("Root element must be <sundex>");

        var layoutElement = rootElement["layout"];
        if (layoutElement == null) throw new XmlException("The <layout> element is required in <sundex>.");

        RootElement = rootElement;
        Layout = new LayoutContainer(this, layoutElement);

        var logicElement = rootElement["logic"];
        var styleElement = rootElement["style"];
        if (logicElement != null) Logic = new LogicContainer(this, logicElement);
        if (styleElement != null) Style = new StyleContainer(this, styleElement);

        Version = rootElement.GetAttribute("version");
        if (Version.Length == 0) Version = "1.0";

        var component = rootElement.GetAttribute("component");
        if (component.Length > 0) Component = component;

        var implements = rootElement.GetAttribute("implements");
        if (implements.Length > 0) Implements = implements;

        Imports           = TryParseListTypeAttribute(rootElement, "imports");
        PartOfCollections = TryParseListTypeAttribute(rootElement, "part-of");
    }

    public XmlElement RootElement { get; }
    public LayoutContainer Layout { get; }
    public LogicContainer? Logic  { get; }
    public StyleContainer? Style  { get; }

    public string         Version           { get; }
    public string?        Component         { get; private set; }
    public string?        Implements        { get; private set; }
    public List<string>   Imports           { get; private set; }
    public List<string>   PartOfCollections { get; init; }
}
```

### Required vs optional

- **Required**: `<layout>` child, root tag must be `<sundex>`.
- **Optional**: `<logic>`, `<style>`, all root attributes (`version`, `component`, `implements`, `imports`, `part-of`).
- **Defaulted**: `Version` defaults to `"1.0"` if missing — that's what tells [[Phases/Component Builders|`ComponentBuilderV1`]] to take responsibility for the document. Future builder versions will add new keys to the `ComponentBuilderVersions` dictionary.

### Root attributes

| Attribute | Type | Meaning |
|---|---|---|
| `version` | `string` | Builder version key. `"1.0"` → `ComponentBuilderV1`. |
| `component` | `string?` | If set, this document defines a reusable component named `<component>`. Empty for top-level documents. |
| `implements` | `string?` | Free-form interface marker. The builder calls `HandleImplements` which currently just registers the component for re-use — see [[Component Definition#registration|Component registration]]. |
| `imports` | `["a", "b"]` | JSON-array string of component names to import. Resolved via `ISundexContext.ResolveComponent`. |
| `part-of` | `["x"]` | JSON-array of collection names this component belongs to. Currently informational. |

### `TryParseListTypeAttribute` — the JSON-in-XML trick

```csharp
private static List<string> TryParseListTypeAttribute(XmlElement rootElement, string attribute)
{
    var imports = rootElement.GetAttribute(attribute);
    if (imports.Length == 0) return [];
    if (!imports.StartsWith('[') || !imports.EndsWith(']'))
        throw new XmlException($"The {attribute} attribute must be a JSON string array.");
    var array = JsonSerializer.Deserialize<List<string>>(imports);
    return array ?? throw new JsonException($"Failed to deserialize {attribute} attribute.");
}
```

`imports="['ui_helpers', 'theme']"` — the value is *literally* a JSON string array embedded in the XML attribute. Bracket markers (`[...]`) are mandatory. Single quotes are accepted because they survive XML attribute encoding without escaping; `System.Text.Json` parses both.

The same trick is used for `part-of` and (in the layout container) `class="[a, b, c]"`.

## `LayoutContainer` — `<layout>` and `BuildTree`

```csharp
public class LayoutContainer(RootContainer root, XmlElement layoutElement)
{
    public List<SundexNode> BuildTree();           // <layout>'s children → SundexNode tree
    private static SundexNode ParseNode(XmlElement element);
}
```

`BuildTree` is what turns the `<layout>` `XmlElement` into a tree of `SundexNode`s — the form the builder consumes:

```csharp
public List<SundexNode> BuildTree()
{
    var nodes = new List<SundexNode>();
    foreach (XmlNode child in LayoutElement.ChildNodes)
        if (child is XmlElement el)
            nodes.Add(ParseNode(el));
    return nodes;
}
```

Recurses via `ParseNode`:

```csharp
private static SundexNode ParseNode(XmlElement element)
{
    var attributes = new Dictionary<string, string>();
    foreach (XmlAttribute attr in element.Attributes)
        attributes[attr.Name] = attr.Value;

    attributes.Remove("id", out var idString);
    attributes.Remove("class", out var classString);

    HashSet<string>? classes = null;
    if (classString is not null) {
        classes = [];
        if (classString.StartsWith('[') && classString.EndsWith(']'))
            classes = classString[1..^1].Split(',').ToHashSet();
        else
            classes.Add(classString);
    }

    var children = new List<SundexNode>();
    foreach (XmlNode child in element.ChildNodes)
        if (child is XmlElement el)
            children.Add(ParseNode(el));

    return new SundexNode {
        TagName    = element.Name,
        Id         = idString,
        Classes    = classes,
        Attributes = attributes,
        Children   = children
    };
}
```

Three things to note:

1. **`id` and `class` are pulled out** of the generic attribute dictionary. They're meaningful to the builder for ID/class registration; everything else is passed through verbatim and dispatched in `ApplyAttributes`.
2. **`class` accepts both forms**: `class="primary"` (single class) or `class="[primary, large]"` (JSON-ish list). The list form uses comma-split — there's no JSON parser here because the values are simple identifiers, no quoting needed.
3. **Text nodes are ignored.** Only `XmlElement` children become `SundexNode`s. Text inside layout (e.g. `<label>Hello</label>`) is *not* read as label text — labels use `value="..."` instead. This avoids the inline-vs-attribute ambiguity.

## `SundexNode` — the intermediate form

```csharp
public class SundexNode
{
    public required string TagName { get; init; }
    public string?         Id      { get; init; }
    public HashSet<string>? Classes { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = [];
    public List<SundexNode> Children { get; init; } = [];
}
```

A node has a tag, optional id/classes, an attribute bag, and children. **No** typed values — `width="50%"` is `Attributes["width"] = "50%"`, the string. Type coercion happens later in [[Phases/Component Builders|`ComponentBuilderV1.ApplyAttributes`]].

This is intentional — the parser stays format-only, doesn't know what tags exist or what their attributes mean. That keeps the parser stable across builder version bumps.

## `StyleContainer` — `<style>`

```csharp
public class StyleContainer(RootContainer root, XmlElement styleElement)
{
    public string SourceCode  { get; private set; } = styleElement.InnerText;
    public string SrcLocation { get; private set; } = styleElement.GetAttribute("src");
    public string Language    { get; }              = styleElement.GetAttribute("language");

    public void UpdateSourceCode(string sourceCode);
}
```

Two ways to specify a stylesheet:

- **Inline**: `<style>...stylesheet text...</style>` — `SourceCode` populated from `InnerText`.
- **External**: `<style src="settings.smxs"/>` — `SrcLocation` set; the builder loads the file and calls `UpdateSourceCode` later.

`Language` is currently unused (only one stylesheet language exists), but reserved.

## `LogicContainer` — `<logic>`

```csharp
public class LogicContainer(RootContainer root, XmlElement logicElement)
{
    public string SourceCode  { get; private set; } = logicElement.InnerText;
    public string SrcLocation { get; private set; } = logicElement.GetAttribute("src");
    public string Language    { get; }              = logicElement.GetAttribute("language");
    public List<string> LanguageImports { get; }    = GetLanguageImports(logicElement.GetAttribute("imports"));

    private static List<string> GetLanguageImports(string imports);
    public void UpdateSourceCode(string value);
}
```

Same shape as `StyleContainer` plus `LanguageImports`. The `language` attribute is **required** for the builder to find a compiler — currently `"csharp"` is the only registered value (see [[Phases/Parsing Logic|Parsing Logic]]).

`GetLanguageImports` accepts either a single import (`imports="MyApp.Settings"`) or a list (`imports="[MyApp.Settings, MyApp.UI]"`). The values are passed straight to Roslyn's `ScriptOptions.AddImports` as namespace imports.

```csharp
private static List<string> GetLanguageImports(string imports)
{
    List<string> importsList = [];
    if (imports.Length == 0) return importsList;
    if (imports.StartsWith('[') && imports.EndsWith(']'))
        importsList = imports[1..^1].Split(',').Select(r => r.Trim()).ToList();
    else
        importsList.Add(imports);
    return importsList;
}
```

## Why `UpdateSourceCode`?

Both `StyleContainer` and `LogicContainer` expose `UpdateSourceCode(string)`. The builder calls it after resolving a `src="..."` attribute:

```csharp
if (!string.IsNullOrEmpty(layout.Style.SrcLocation)) {
    var newSource = context.UIContext.AssetProvider
        .Load<StringAsset, StringInfo>(StringInfo.CreateFromUnknownStorage(src));
    layout.Style.UpdateSourceCode(newSource.Value);
}
```

This way the parsing stage stays I/O-free — `MarkupParser.Parse` is a pure string-to-tree transform. Only the builder, which has access to the `AssetProvider`, knows how to fetch external sources.

The setter being `private set` means only `UpdateSourceCode` can mutate `SourceCode` after construction — keeping the assignment explicit rather than allowing arbitrary writes.

## Threading

`MarkupParser.Parse` is **pure** — no GL calls, no I/O, no shared state. Safe to call from any thread. Same for the `*Container` constructors — they walk the `XmlDocument` and copy attributes, that's it.

The actual realisation happens in [[Phases/Component Builders|`ComponentBuilderV1.CreateComponent`]], which *does* touch GL state (creates `UIElement`s with GPU buffers). That step must run on the GL thread.

Production code that loads markup from disk typically does:

```csharp
ThreadRunner.RunTask(() => {
    var markup = File.ReadAllText(path);
    var document = MarkupParser.Parse(markup);            // off-thread, fine
    Game.Enqueue(_ => {
        var component = sundexContext.NewComponent(markup); // GL-thread builder
        scene.Root.AddChild(component.Element);
    });
});
```

## Related

- [[Component Definition|Component Definition]] — what `SundexComponent`s and `SundexContext` actually represent.
- [[Phases/Component Builders|Component Builders]] — what consumes the `SundexDocument`.
- [[Phases/Parsing Markup|Parsing Markup]] — how `SundexNode`s become `UIElement`s.
- [[../Components/Components|Components]] — the target types.
