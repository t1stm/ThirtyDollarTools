using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
using Sunder.Markup.Abstract;
using Sunder.Markup.Document;
using Sunder.Markup.Layout;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Core;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sunder.Markup.Builders;

public class ComponentBuilderV1 : IComponentBuilder
{
    public const string Version = "1.0";

    public SundexComponent CreateComponent(SundexDocument layout, ISundexContext context)
    {
        var root = layout.Root;
        List<ISundexComponent>? dependencies = null;
        if (root.Imports.Count > 0)
            dependencies = HandleDependencies(layout, context);

        var tree = layout.Layout.BuildTree();

        StyleSheet? styleSheet = null;
        if (layout.Style is not null)
        {
            var styleSheetHolder = StyleParser.Parse(layout.Style.StyleString, path =>
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

        var uiElement = BuildUIElement(rootNode, context.UIContext, dependencies, styleSheet,
            registeredIds, registeredClasses);

        if (styleSheet is not null)
            uiElement.ApplyStyleSheet(styleSheet);

        if (layout.Logic is not null)
        {
            // TODO
        }

        var component = new SundexComponent
        {
            Version = Version,
            Context = context,
            Element = uiElement,
            RegisteredIDs = registeredIds,
            RegisteredClasses = registeredClasses
        };

        if (root.Implements?.Length > 0)
            HandleImplements(component, context);
        return component;
    }

    private static UIElement BuildUIElement(
        SundexNode node,
        UIContext context,
        List<ISundexComponent>? dependencies,
        StyleSheet? styleSheet,
        Dictionary<string, UIElement> registeredIds,
        Dictionary<string, List<UIElement>> registeredClasses)
    {
        var nodeTag = node.TagName;
        UIElement element;
        switch (nodeTag)
        {
            case "stack":
            {
                element = new StackPanel(context)
                {
                    Children = node.Children
                        .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                            registeredClasses))
                        .ToList()
                };
                break;
            }

            case "flex":
            {
                element = new FlexPanel(context)
                {
                    Children = node.Children
                        .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                            registeredClasses))
                        .ToList()
                };
                break;
            }

            case "panel":
            {
                element = new Panel(context)
                {
                    Children = node.Children
                        .Select(child => BuildUIElement(child, context, dependencies, styleSheet, registeredIds,
                            registeredClasses))
                        .ToList()
                };
                break;
            }

            case "label":
            {
                node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue("value", out var text);
                element = new Label(context, text ?? string.Empty);
                break;
            }

            case "progress":
            {
                var background = ExtractBackgroundStyle(node, styleSheet);
                var foreground = ExtractBackgroundStyle(node, styleSheet, "foreground");

                if (node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>()
                        .TryGetValue("value", out var progressString) &&
                    float.TryParse(progressString, out var progress))
                    element = new ProgressBar(context, background, foreground)
                    {
                        Progress = progress
                    };
                else
                    element = new ProgressBar(context, background, foreground);

                break;
            }

            case "button":
            {
                var labelNode = node.Children.FirstOrDefault(child => child.TagName == "label");
                if (labelNode is null) throw new Exception("Button must have a label");

                var background = ExtractBackgroundStyle(node, styleSheet);
                element = BuildUIElement(labelNode, context, dependencies, styleSheet, registeredIds, registeredClasses)
                    is Label label
                    ? new Button(context, label, background)
                    : throw new Exception("Button label wasn't parsed as a label.");
                break;
            }
            default:
            {
                if (dependencies is null) throw new Exception($"Unknown tag: {nodeTag}");
                var dependency = dependencies
                    .FirstOrDefault(dependency => dependency.Name == nodeTag);

                element = dependency is not null
                    ? dependency.Element
                    : throw new Exception($"Unknown node tag: {nodeTag}");
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
                return new LiteralOrComputable(v, false);
        }
        else
        {
            if (float.TryParse(value, out var v))
                return new LiteralOrComputable(v, false);
        }

        return new LiteralOrComputable(0, false);
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

    private static IStyleValue? GetStyleForNode(SundexNode node, string property, StyleSheet? styleSheet)
    {
        if (styleSheet is null) return null;
        var tagName = node.TagName;
        return styleSheet.GetStyleValueForTag(tagName, property);
    }

    private static List<ISundexComponent> HandleDependencies(SundexDocument layout, ISundexContext context)
    {
        return layout.Root.Imports.Select(import => context.ResolveComponent(import)).ToList();
    }

    private static void HandleImplements(SundexComponent component, ISundexContext context)
    {
        context.RegisterComponent(component);
    }
}