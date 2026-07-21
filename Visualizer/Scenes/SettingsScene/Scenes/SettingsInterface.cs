using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
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
    private static readonly Vector4 ColorInputBackground = new(0.15f, 0.16f, 0.21f, 1f);

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
            ? CreateCheckbox(context, settings, property)
            : property.PropertyType == typeof(int) || property.PropertyType == typeof(float)
                ? CreateNumericInput(context, settings, property)
                : CreateTextInput(context, settings, property);

        row.Children = [nameLabel, valueWidget];
        return row;
    }

    private static Checkbox CreateCheckbox(UIContext context, VisualizerSettings settings, PropertyInfo property)
    {
        var isOn = (bool)(property.GetValue(settings) ?? false);
        var checkbox = new Checkbox(context, "", isOn);
        checkbox.OnCheckedChanged = box => property.SetValue(settings, box.Checked);
        return checkbox;
    }

    private static NumericInput CreateNumericInput(UIContext context, VisualizerSettings settings, PropertyInfo property)
    {
        var isInt = property.PropertyType == typeof(int);
        var current = Convert.ToDouble(property.GetValue(settings));
        var (min, max, step) = NumericRangeFor(property.Name);

        var input = new NumericInput(context, current)
        {
            Width = 140f,
            Height = 32f,
            FontSizePx = 14f,
            Min = min,
            Max = max,
            Step = step,
            Filter = isInt ? TextInputFilter.Integer : TextInputFilter.Decimal,
            BorderRadius = 6f,
            Background = new ColoredPlane { Color = ColorInputBackground }
        };

        input.OnValueChanged = _ =>
        {
            if (input.Value is not { } value) return;
            property.SetValue(settings, isInt ? (int)Math.Round(value) : (float)value);
        };

        return input;
    }

    private static (double Min, double Max, double Step) NumericRangeFor(string propertyName) => propertyName switch
    {
        nameof(VisualizerSettings.EventSize) => (16, 256, 1),
        nameof(VisualizerSettings.EventMargin) => (0, 128, 1),
        nameof(VisualizerSettings.LineAmount) => (1, 64, 1),
        nameof(VisualizerSettings.ScrollSpeed) => (0.1, 100, 0.5),
        _ => (double.MinValue, double.MaxValue, 1)
    };

    private static TextInput CreateTextInput(UIContext context, VisualizerSettings settings, PropertyInfo property)
    {
        var current = property.GetValue(settings) as string ?? "";
        var input = new TextInput(context, current)
        {
            Width = 220f,
            FontSizePx = 14f,
            BorderRadius = 6f,
            Background = new ColoredPlane { Color = ColorInputBackground }
        };

        input.OnValueChanged = i => property.SetValue(settings, i.Value);
        return input;
    }

    private static string FormatPropertyName(string name)
    {
        return Regex.Replace(name, "([A-Z])", " $1").TrimStart();
    }
}