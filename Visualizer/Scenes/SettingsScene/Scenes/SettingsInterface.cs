using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using VisualizerScene.Settings;

namespace SettingsScene.Scenes;

public class SettingsInterface
{
    private static readonly Vector4 ColorText = new(0.84f, 0.85f, 0.86f, 1f);
    private static readonly Vector4 ColorMuted = new(0.54f, 0.59f, 0.69f, 1f);
    private static readonly Vector4 ColorToggleOn = new(0.48f, 0.64f, 0.97f, 1f);
    private static readonly Vector4 ColorToggleOff = new(0.25f, 0.28f, 0.41f, 1f);

    public SettingsInterface(UIContext context, VisualizerSettings settings, Action back)
    {
        OnBack = back;

        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = "Scenes/Layout/SettingsInterface.snx.xml" }
        });

        Component = sundexContext.NewComponent(componentSource.Value);
        sundexContext.RunLogicAndVerify(Component, () => RootPanel, () => SettingsList);

        PopulateSettingRows(context, settings);

        RootPanel.DrawTo(context);
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    [SetFromLogic] public Panel RootPanel { get; set; } = null!;
    [SetFromLogic] public FlexPanel SettingsList { get; set; } = null!;

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(UIContext context)
    {
        RootPanel.Update(context);
        RootPanel.Layout();
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
    }

    // To add categories in the future: group properties by a category attribute or a lookup table,
    // then call AddCategorySection(label) followed by AddSettingRow per property in each group.
    private void PopulateSettingRows(UIContext context, VisualizerSettings settings)
    {
        var properties = settings.GetType().GetProperties();
        foreach (var property in properties)
            SettingsList.AddChild(CreateSettingRow(context, settings, property));
    }

    private static FlexPanel CreateSettingRow(UIContext context, VisualizerSettings settings, PropertyInfo property)
    {
        var row = new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            VerticalAlign = Align.Center,
            Spacing = 12f,
            Height = 44f
        };

        var nameLabel = new Label(context, FormatPropertyName(property.Name))
        {
            FontSizePx = 16f,
            Color = ColorText
        };

        UIElement valueWidget = property.PropertyType == typeof(bool)
            ? CreateToggleButton(context, settings, property)
            : CreateValueLabel(context, settings, property);

        row.Children = [nameLabel, valueWidget];
        return row;
    }

    private static Button CreateToggleButton(UIContext context, VisualizerSettings settings, PropertyInfo property)
    {
        var isOn = (bool)(property.GetValue(settings) ?? false);

        var button = new Button(context, isOn ? "ON" : "OFF")
        {
            Width = 80f,
            Height = 36f,
            BorderRadius = 8f,
            Background = new ColoredPlane { Color = isOn ? ColorToggleOn : ColorToggleOff },
            FontSizePx = 16f,
            Label =
            {
                Color = Vector4.One
            }
        };

        button.OnClick = _ =>
        {
            var current = (bool)(property.GetValue(settings) ?? false);
            var next = !current;
            property.SetValue(settings, next);
            button.Label.Value = next ? "ON" : "OFF";
            button.Background = new ColoredPlane { Color = next ? ColorToggleOn : ColorToggleOff };
        };

        return button;
    }

    private static Label CreateValueLabel(UIContext context, VisualizerSettings settings, PropertyInfo property)
    {
        return new Label(context, property.GetValue(settings)?.ToString() ?? "(none)")
        {
            FontSizePx = 16f,
            Color = ColorMuted
        };
    }

    private static string FormatPropertyName(string name)
    {
        return Regex.Replace(name, "([A-Z])", " $1").TrimStart();
    }
}