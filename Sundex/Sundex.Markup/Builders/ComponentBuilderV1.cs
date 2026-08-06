using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Core;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup.Abstract;
using Sundex.Markup.Document;
using Sundex.Markup.Layout;
using Sundex.Markup.Logic;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace Sundex.Markup.Builders;

public class ComponentBuilderV1 : IComponentBuilder
{
    public const string Version = "1.0";

    public SundexComponent CreateComponent(SundexDocument layout, ISundexContext context)
    {
        return CreateComponent(layout, context, true);
    }

    /// <param name="register">
    ///     False when building a usage site of an already-registered component: the rebuild
    ///     must not replace (or collide with) the registered template it was built from.
    /// </param>
    private SundexComponent CreateComponent(SundexDocument layout, ISundexContext context, bool register)
    {
        var root = layout.Root;
        List<ISundexComponent>? dependencies = null;
        if (root.Imports.Count > 0)
            dependencies = HandleDependencies(layout, context);

        var tree = layout.Layout.BuildTree();

        StyleSheet? styleSheet = null;
        if (layout.Style is not null)
        {
            var src = layout.Style.SrcLocation;
            if (!string.IsNullOrEmpty(src))
            {
                var newSource =
                    context.UIContext.AssetProvider.Load<StringAsset, StringInfo>(
                        StringInfo.CreateFromUnknownStorage(src));
                layout.Style.UpdateSourceCode(newSource.Value);
            }

            var styleSheetHolder = StyleParser.Parse(layout.Style.SourceCode, path =>
            {
                var assetStream = context.UIContext.AssetProvider
                    .Load<AssetStream, AssetInfo>(new AssetInfo
                    {
                        Location = path
                    });

                using var assetString = assetStream.Stream;
                using var stringReader = new StreamReader(assetString);
                return stringReader.ReadToEnd();
            });

            styleSheet = new StyleSheet(styleSheetHolder);
        }

        if (tree.Count != 1)
            throw new Exception("Only one root element is supported");

        var rootNode = tree[0];
        var registeredIds = new Dictionary<string, UIElement>(StringComparer.Ordinal);
        var registeredClasses = new Dictionary<string, List<UIElement>>(StringComparer.Ordinal);
        var children = new List<SundexComponent>();

        var uiElement = BuildUIElement(rootNode, context, dependencies, styleSheet,
            registeredIds, registeredClasses, children);

        if (styleSheet is not null)
            uiElement.ApplyStyleSheet(styleSheet);

        var logic = layout.Logic;
        Action<object?>? runLogic = null;

        var component = new SundexComponent
        {
            Version = Version,
            Context = context,
            Element = uiElement,
            Document = layout,
            Name = root.Component ?? root.Implements,
            RegisteredIDs = registeredIds,
            RegisteredClasses = registeredClasses,
            StyleSheet = styleSheet,
            Dependencies = dependencies is null ? [] : [..dependencies]
        };

        component.Children.AddRange(children);

        if (logic is not null)
        {
            LanguageProvider.Languages.TryGetValue(logic.Language, out var language);
            if (language is null)
                throw new NotSupportedException($"Language {logic.Language} is not supported.");

            if (!string.IsNullOrEmpty(logic.SrcLocation))
            {
                var newSource =
                    context.UIContext.AssetProvider.Load<StringAsset, StringInfo>(
                        StringInfo.CreateFromUnknownStorage(logic.SrcLocation));
                logic.UpdateSourceCode(newSource.Value);
            }

            runLogic = language.Compile(logic.SourceCode, context, component, logic.LanguageImports);
        }

        // Imported sub-components carry their own logic bound to their own id map. Cascade
        // depth-first so a child is fully wired before the host's logic reaches for it.
        component.RunLogic = children.Count == 0
            ? runLogic
            : obj =>
            {
                foreach (var child in children) child.RunLogic?.Invoke(obj);
                runLogic?.Invoke(obj);
            };

        if (register && component.Name is not null)
            context.RegisterComponent(component);
        return component;
    }

    private UIElement BuildUIElement(
        SundexNode node,
        ISundexContext context,
        List<ISundexComponent>? dependencies,
        StyleSheet? styleSheet,
        Dictionary<string, UIElement> registeredIds,
        Dictionary<string, List<UIElement>> registeredClasses,
        List<SundexComponent> children)
    {
        var nodeTag = node.TagName;
        UIElement element;
        switch (nodeTag)
        {
            case "stack":
            {
                element = new StackPanel(context.UIContext)
                {
                    Children =
                    [
                        .. node.Children
                            .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                                registeredClasses, children))
                    ]
                };
                break;
            }

            case "flex":
            {
                element = new FlexPanel(context.UIContext)
                {
                    Children =
                    [
                        .. node.Children
                            .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                                registeredClasses, children))
                    ]
                };
                break;
            }

            case "panel":
            {
                element = new Panel(context.UIContext)
                {
                    Children =
                    [
                        .. node.Children
                            .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                                registeredClasses, children))
                    ]
                };
                break;
            }

            case "label":
            {
                node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue("value", out var text);
                element = new Label(context.UIContext, text ?? string.Empty);
                break;
            }

            case "progress":
            {
                var background = ExtractBackgroundStyle(node, styleSheet);
                var foreground = ExtractBackgroundStyle(node, styleSheet, "foreground");

                if (node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>()
                        .TryGetValue("value", out var progressString) &&
                    float.TryParse(progressString, out var progress))
                    element = new ProgressBar(context.UIContext, background, foreground)
                    {
                        Progress = progress
                    };
                else
                    element = new ProgressBar(context.UIContext, background, foreground);

                break;
            }

            case "scroll-view":
            {
                element = new ScrollView(context.UIContext)
                {
                    Children =
                    [
                        .. node.Children
                            .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                                registeredClasses, children))
                    ]
                };
                break;
            }

            case "modal":
            {
                element = new ModalLayer(context.UIContext)
                {
                    Children =
                    [
                        .. node.Children
                            .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                                registeredClasses, children))
                    ]
                };
                break;
            }

            case "text-input":
            {
                node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue("value", out var value);
                element = new TextInput(context.UIContext, value ?? string.Empty);
                break;
            }

            case "numeric-input":
            {
                node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue("value", out var value);
                element = new NumericInput(context.UIContext,
                    double.TryParse(value, out var number) ? number : null);
                break;
            }

            case "checkbox":
            {
                var attributes = node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>();
                // `value` is the label text, matching <label value=>; `checked` is the state.
                attributes.TryGetValue("value", out var label);
                attributes.TryGetValue("checked", out var state);
                element = new Checkbox(context.UIContext, label ?? string.Empty,
                    bool.TryParse(state, out var isChecked) && isChecked);
                break;
            }

            case "slider":
            {
                // Slider derives from ProgressBar and takes the same two planes.
                var background = ExtractBackgroundStyle(node, styleSheet);
                var foreground = ExtractBackgroundStyle(node, styleSheet, "foreground");

                var slider = new Slider(context.UIContext, background, foreground);
                if (node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue("value", out var sliderValue)
                    && double.TryParse(sliderValue, out var parsed))
                    slider.Value = parsed;

                element = slider;
                break;
            }

            case "button":
            {
                var labelNode = node.Children.FirstOrDefault(child => child.TagName == "label");
                if (labelNode is null) throw new Exception("Button must have a label");

                var background = ExtractBackgroundStyle(node, styleSheet);
                element = BuildUIElement(labelNode, context, dependencies, styleSheet, registeredIds, registeredClasses,
                children)
                    is Label label
                    ? new Button(context.UIContext, label, background)
                    : throw new Exception("Button label wasn't parsed as a label.");
                break;
            }
            default:
            {
                // Check for custom element factories first
                var customElement = context.CreateElement(nodeTag);

                if (customElement != null)
                {
                    element = customElement;
                    if (element is Panel panel && node.Children.Count > 0)
                        panel.Children.AddRange(node.Children
                            .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                                registeredClasses, children)));
                    break;
                }

                if (dependencies is null) throw new Exception($"Unknown tag: {nodeTag}");
                var dependency = dependencies
                    .FirstOrDefault(dependency => dependency.Name == nodeTag);

                if (dependency is null) throw new Exception($"Unknown node tag: {nodeTag}");

                // Rebuild rather than alias: handing out the registered component's own
                // Element made every usage site the same instance, so a second <header/>
                // reparented the first one out of the tree. Building the retained document
                // again is independent - BuildTree reparses from the held XmlElement - and
                // it binds the sub-component's logic to its own id map, which a shared
                // instance could never do.
                // ponytail: single builder version exists, so a dependency declaring a
                // different version than its host would still be built by this one.
                var instance = CreateComponent(dependency.Document, context, false);
                children.Add(instance);
                element = instance.Element;
                break;
            }
        }

        ApplyAttributes(element, node);

        if (node.Id is not null) element.ID = node.Id;

        if (node.Classes is not null) element.Classes = node.Classes;

        // Register ID
        if (!string.IsNullOrEmpty(node.Id)) registeredIds[node.Id] = element;

        // Register classes
        if (node.Classes is not { Count: > 0 }) return element;

        foreach (var @class in node.Classes)
        {
            if (!registeredClasses.TryGetValue(@class, out var list))
            {
                list = [];
                registeredClasses[@class] = list;
            }

            list.Add(element);
        }

        return element;
    }

    private static void ApplyAttributes(UIElement element, SundexNode node)
    {
        foreach (var (key, value) in node.Attributes)
            switch (key)
            {
                case "width":
                    element.Width = ParseLiteralOrComputable(value);
                    break;
                case "height":
                    element.Height = ParseLiteralOrComputable(value);
                    break;
                case "padding":
                    if (float.TryParse(value, out var p))
                        if (element is IPositioningElement pe)
                            pe.Padding = p;
                    break;
                case "spacing":
                    if (float.TryParse(value, out var s))
                        switch (element)
                        {
                            case StackPanel sp:
                                sp.Spacing = s;
                                break;
                            case FlexPanel fp:
                                fp.Spacing = s;
                                break;
                        }

                    break;
                case "direction":
                    if (Enum.TryParse<LayoutDirection>(value, true, out var dir))
                        switch (element)
                        {
                            case StackPanel sp:
                                sp.Direction = dir;
                                break;
                            case FlexPanel fp:
                                fp.Direction = dir;
                                break;
                        }

                    break;
            }
    }

    private static LiteralOrComputable ParseLiteralOrComputable(string value)
    {
        if (value.EndsWith('%'))
        {
            if (float.TryParse(value[..^1], out var p))
                return new LiteralOrComputable(p, true);
        }
        else if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return new LiteralOrComputable(0, false, true);
        }
        else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(value[..^2], out var v))
                return new LiteralOrComputable(v);
        }
        else
        {
            if (float.TryParse(value, out var v))
                return new LiteralOrComputable(v);
        }

        return new LiteralOrComputable(0);
    }

    private static Renderable? ExtractBackgroundStyle(SundexNode node, StyleSheet? styleSheet,
        string property = "background")
    {
        var background = GetStyleForNode(node, property, styleSheet);
        return background switch
        {
            GradientValue gv => gv.GenerateGradientPlane(),
            ColorValue cv => new ColoredPlane { Color = cv.Vector },
            _ => null
        };
    }

    /// <summary>
    ///     Resolves a style property for a node the same way <c>UIElement.SetNamedSetting</c>
    ///     does: tag, then classes, then id, last hit winning. This was tag-only, so a
    ///     class- or id-scoped <c>background</c> silently never reached the elements that
    ///     take their planes as constructor arguments (progress, slider, button) - the
    ///     reason those bars were built in code with hand-made <c>ColoredPlane</c>s.
    /// </summary>
    private static IStyleValue? GetStyleForNode(SundexNode node, string property, StyleSheet? styleSheet)
    {
        if (styleSheet is null) return null;

        var value = styleSheet.GetStyleValueForTag(node.TagName, property);

        if (node.Classes is not null)
            foreach (var @class in node.Classes)
                value = styleSheet.GetStyleValueForTag(@class, property) ?? value;

        if (node.Id is not null)
            value = styleSheet.GetStyleValueForTag(node.Id, property) ?? value;

        return value;
    }

    private static List<ISundexComponent> HandleDependencies(SundexDocument layout, ISundexContext context)
    {
        return [.. layout.Root.Imports.Select(import => context.ResolveComponent(import))];
    }
}