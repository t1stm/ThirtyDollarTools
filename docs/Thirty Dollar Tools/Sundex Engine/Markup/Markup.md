# Markup

Sundex's **markup language** (`.smxl`) is XML with three sections — `<layout>`, `<style>`, `<logic>` — under a `<sundex>` root. It's how UI trees, stylesheets, and logic scripts are described on disk and loaded into the app at runtime.

> Source: `Sundex/Sundex.Markup/`.

## A complete example

```xml
<?xml version="1.0" encoding="utf-8"?>
<sundex version="1.0" imports="['ui_helpers']" component="settings_page">
    <layout>
        <flex direction="vertical" padding="10" spacing="10" id="root">
            <label value="Settings" id="title"/>
            <button id="save_btn">
                <label value="Save"/>
            </button>
        </flex>
    </layout>

    <style src="settings.smxs"/>

    <logic language="csharp" imports="MyApp.Settings">
        var saveBtn = Component.GetID&lt;Button&gt;("save_btn");
        saveBtn.OnClick = _ =&gt; SettingsManager.Save();
    </logic>
</sundex>
```

Three sections, three concerns:

- **`<layout>`** — the UI tree. Tags map to [[../Components/Components|component classes]] (`<flex>` → `FlexPanel`, `<button>` → `Button`, etc.).
- **`<style>`** — a [[../Style DSL/Style DSL|stylesheet]], either inline as element text or referenced via `src="..."`.
- **`<logic>`** — script source code (Roslyn-compiled C# right now), runs once after the tree is built. Used to wire up event handlers and apply runtime data.

## What's in this folder

```
Markup/
├── MarkupParser.cs            ← XML → SundexDocument
├── SundexContext.cs           ← runtime registry of components, builders, factories
├── SundexComponent.cs         ← built tree + ID/class lookups + RunLogic delegate
├── Layout/
│   └── SundexNode.cs          ← intermediate node (tag, id, classes, attrs, children)
├── Document/
│   ├── SundexDocument.cs      ← Root → Layout/Logic/Style
│   ├── Root/RootContainer.cs  ← <sundex> attributes (version, imports, component, ...)
│   ├── Layout/LayoutContainer.cs
│   ├── Logic/LogicContainer.cs
│   └── Style/StyleContainer.cs
├── Builders/
│   └── ComponentBuilderV1.cs  ← turns a SundexDocument into a real UI tree
├── Logic/
│   ├── LanguageProvider.cs
│   └── Languages/
│       ├── SundexScript.cs    ← abstract script compiler base
│       └── CSharp/CSharp.cs   ← Roslyn-backed C# scripting
├── Abstract/                  ← ISundexComponent, ISundexContext, IComponentBuilder
└── Attributes/
    └── SetFromLogicAttribute.cs
```

## The pipeline at a glance

```
   markup string (file or hardcoded)
      │
      │  MarkupParser.Parse           ← System.Xml.XmlDocument
      ▼
   SundexDocument
      ├── RootContainer       (<sundex version, imports, component, implements>)
      ├── LayoutContainer     (<layout>...)
      ├── StyleContainer?     (<style src or inline>)
      └── LogicContainer?     (<logic language="csharp">code</logic>)
      │
      │  context.NewComponent → ComponentBuilderV1.CreateComponent
      ▼
   SundexComponent
      ├── Element (UIElement)        ← the root of the realised UI tree
      ├── RegisteredIDs              ← Dictionary<string, UIElement>
      ├── RegisteredClasses          ← Dictionary<string, List<UIElement>>
      ├── RunLogic: Action<object?>?  ← compiled script delegate
      └── Dependencies               ← imported components
```

`SundexContext` is the runtime registry that sits over all of this — it holds builder versions, registered components, and custom element factories. One `SundexContext` per [[../Engine/Scene Management|scene]] (or per app, depending on usage).

## Where to read next

This folder breaks down into four logical concerns; each has its own page:

1. **[[Markup Parser|Markup Parser]]** — `MarkupParser`, `SundexDocument`, the `RootContainer`/`LayoutContainer`/`StyleContainer`/`LogicContainer` quartet, and `SundexNode`. The "XML → in-memory document" stage.

2. **[[Component Definition|Component Definition]]** — `ISundexComponent`, `ISundexContext`, `SundexComponent`, `SundexContext`, the `[SetFromLogic]` attribute. The "what is a component, how do they connect to each other" layer.

3. **[[Phases/Phases|Phases]]** — `IComponentBuilder` + `ComponentBuilderV1`, plus the per-section parsing logic (Component Builders, Parsing Markup, Parsing Style, Parsing Logic). The actual "document → realised UI tree" pipeline.

## Relationship to other modules

- **[[../Components/Components|Components]]** — the markup builder instantiates these.
- **[[../Style DSL/Style DSL|Style DSL]]** — `StyleParser.Parse` is what `<style>` runs through.
- **[[../Engine/Asset Management|Asset Management]]** — `src="..."` in `<style>` and `<logic>` resolve through `AssetProvider.Load<StringAsset, StringInfo>`.
- **[[../Engine/Threading|Threading]]** — Roslyn compilation is heavy; production code runs `context.NewComponent(...)` via `ThreadRunner.RunTask` and rejoins the GL thread via `Game.Enqueue` to commit the realised tree.

## Why XML? Why not JSON / TOML / a custom format?

- **`System.Xml`** ships in BCL — zero new dependencies.
- **Three-section structure** is naturally tree-shaped, and XML's mixed content (text inside `<logic>`) handles the script case without weird escaping.
- **Attributes vs children** — every UI tag has a clear set of attributes (`id`, `class`, `width`, `height`, ...) plus children. JSON or TOML would either bloat the syntax or require a custom flatten.
- **Familiar** — anyone who's written HTML or WPF/XAML can read `.smxl` immediately.

The trade-offs (verbosity, no comments-with-attributes, no integers vs strings distinction) are all minor for the use case.

## Related

- [[../Components/Components|Components]] — the layer this module instantiates.
- [[../Style DSL/Style DSL|Style DSL]] — the language inside `<style>`.
- The Visualizer's `*.smxl` files in `Visualizer.Game/Assets/Markup/` are the largest in-tree consumers.
