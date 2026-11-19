using System.Globalization;
using OpenTK.Mathematics;
using Sundex.Core.Animations;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL;

public class StyleSheet(StyleSheetHolder holder)
{
    public Dictionary<string, KeyframedAnimation> ComputedAnimations { get; } = ParseAnimations(holder);
    public Dictionary<string, Dictionary<string, IStyleValue>> Components { get; } = holder.Components;
    public Dictionary<string, Dictionary<string, IStyleValue>> Classes { get; } = holder.Classes;
    public Dictionary<string, Dictionary<string, IStyleValue>> IDTags { get; } = holder.IDTags;

    private static Dictionary<string, KeyframedAnimation> ParseAnimations(StyleSheetHolder holder)
    {
        var animations = new Dictionary<string, KeyframedAnimation>();

        foreach (var (animationName, values) in holder.Animations)
        {
            var keyframes = new List<Keyframe>();
            var keyframesValue = values.TryGetValue("keyframes", out var keyframesStyleValue)
                ? (KeyframesValue)keyframesStyleValue
                : null;

            if (keyframesValue is null) continue;

            var globalSteppingFunction = SteppingFunction.Linear;
            if (values.TryGetValue("timing-function", out var steppingFunctionValue) &&
                steppingFunctionValue is StringValue stringValue)
                globalSteppingFunction = SteppingFunctions.ParseSteppingFunction(stringValue.Value);

            var globalLength = 0;
            if (values.TryGetValue("duration", out var lengthValue) && lengthValue is NumberValue numberValue)
                globalLength = numberValue.Unit switch
                {
                    "ms" => (int)numberValue.Value,
                    "s" => (int)(numberValue.Value * 1000),
                    "m" => (int)(numberValue.Value * 60000),
                    _ => throw new ArgumentException($"Invalid length unit {numberValue.Unit}")
                };

            if (globalLength < 1)
                throw new ArgumentException("Keyframes length must be positive");

            var previousPercentage = 0.0;
            foreach (var (percentage, propertiesBlock) in keyframesValue.Keyframes)
            {
                var deltaPct = percentage - previousPercentage;
                if (deltaPct < 0) deltaPct = 0;
                previousPercentage = percentage;

                var keyframe = new Keyframe
                {
                    SteppingFunction = globalSteppingFunction,
                    LengthMs = (float)(globalLength * deltaPct)
                };

                foreach (var (property, value) in propertiesBlock)
                    ParseKeyframeProperties(property, value, ref keyframe);
                keyframes.Add(keyframe);
            }

            var keyframed = new KeyframedAnimation(keyframes);
            animations.Add(animationName, keyframed);
        }

        return animations;
    }

    private static void ParseKeyframeProperties(string property, IStyleValue value, ref Keyframe keyframe)
    {
        switch (property)
        {
            case "timing-function" when value is StringValue steppingFunctionString:
            {
                keyframe.SteppingFunction =
                    SteppingFunctions.ParseSteppingFunction(steppingFunctionString.Value);
                break;
            }

            case "transform" when value is VectorValue vectorValue:
            {
                keyframe.Position = vectorValue.Count switch
                {
                    2 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, 0),
                    3 => new Vector3((float)vectorValue.X, (float)vectorValue.Y,
                        (float)(vectorValue.Z ?? 0)),
                    _ => throw new ArgumentException("Invalid vector count for transform property")
                };
                break;
            }

            case "opacity" when value is NumberValue numberValue:
            {
                keyframe.Opacity = numberValue.Unit is "%" ? numberValue.Value / 100f : numberValue.Value;
                break;
            }

            case "color" when value is ColorValue colorValue:
            {
                keyframe.Color = colorValue.Vector;
                break;
            }

            case "loop" when value is StringValue loopString:
            {
                keyframe.LoopingMode = loopString.Value switch
                {
                    "none" => AnimationLoopingMode.None,
                    "invert" => AnimationLoopingMode.Invert,
                    "loop-start" => AnimationLoopingMode.LoopStart,
                    "reset" => AnimationLoopingMode.ResetToStart,
                    _ => throw new ArgumentException("Invalid loop mode")
                };
                break;
            }

            case "scale" when value is VectorValue vectorValue:
            {
                keyframe.Scale = vectorValue.Count switch
                {
                    2 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, 1),
                    3 => new Vector3((float)vectorValue.X, (float)vectorValue.Y,
                        (float)(vectorValue.Z ?? 1)),
                    _ => throw new ArgumentException("Invalid scale vector length")
                };
                break;
            }
        }
    }

    public IStyleValue? GetStyleValueForTag(string name, string property)
    {
        var ids = IDTags.GetAlternateLookup<ReadOnlySpan<char>>();
        var classes = Classes.GetAlternateLookup<ReadOnlySpan<char>>();
        var components = Components.GetAlternateLookup<ReadOnlySpan<char>>();

        if (ids.TryGetValue(name, out var idProps) && idProps.TryGetValue(property, out var idValue)) return idValue;
        if (classes.TryGetValue(name, out var classProps) && classProps.TryGetValue(property, out var classValue))
            return classValue;
        if (components.TryGetValue(name, out var componentProps) &&
            componentProps.TryGetValue(property, out var componentValue)) return componentValue;
        return null;
    }

    /// <summary>
    ///     Returns the property overrides for a given state on a tag (id, class, or component),
    ///     or null if no state block is defined for that tag/state combination.
    /// </summary>
    public Dictionary<string, IStyleValue>? GetStateOverrideForTag(string name, string state)
    {
        if (state.Length > 1024)
            throw new ArgumentException("State name is too long.");

        Span<char> keySpan = stackalloc char[state.Length + "state[]".Length];
        keySpan.TryWrite($"state[{state}]", out _);
        ReadOnlySpan<char> readonlyKeySpan = keySpan;

        var ids = IDTags.GetAlternateLookup<ReadOnlySpan<char>>();
        var classes = Classes.GetAlternateLookup<ReadOnlySpan<char>>();
        var components = Components.GetAlternateLookup<ReadOnlySpan<char>>();

        if (ids.TryGetValue(name, out var idProps) && idProps.GetAlternateLookup<ReadOnlySpan<char>>()
                .TryGetValue(readonlyKeySpan, out var idState) &&
            Block(idState) is { } idBlock) return idBlock.Properties;
        if (classes.TryGetValue(name, out var classProps) && classProps.GetAlternateLookup<ReadOnlySpan<char>>()
                .TryGetValue(readonlyKeySpan, out var classState) &&
            Block(classState) is { } classBlock) return classBlock.Properties;
        if (components.TryGetValue(name, out var componentProps) &&
            componentProps.GetAlternateLookup<ReadOnlySpan<char>>()
                .TryGetValue(readonlyKeySpan, out var componentState) &&
            Block(componentState) is { } componentBlock) return componentBlock.Properties;
        return null;
    }

    /// <summary>A state or media block, written plain or as <c>!override { … }</c>, which once went unread.</summary>
    private static BlockValue? Block(IStyleValue value) => value switch
    {
        BlockValue block => block,
        OverrideValue { Value: BlockValue block } => block,
        _ => null
    };

    private readonly Dictionary<string, MediaRule[]> _media = new(StringComparer.Ordinal);

    /// <summary>
    ///     The <c>media[…]</c> blocks of a tag, class or id, in the order written. Empty when it has none,
    ///     which is most of them, so this is cached per name.
    /// </summary>
    public MediaRule[] GetMediaRulesForTag(string name)
    {
        if (_media.TryGetValue(name, out var rules)) return rules;
        var found = new List<MediaRule>();
        foreach (var source in new[] { Components, Classes, IDTags })
            if (source.TryGetValue(name, out var properties))
                foreach (var (key, value) in properties)
                    if (key.StartsWith("media[", StringComparison.Ordinal) && Block(value) is { } block)
                        found.Add(new MediaRule(MediaCondition.Parse(key[6..^1]), block.Properties));
        return _media[name] = [.. found];
    }
}

/// <summary>A <c>media[…]</c> block: when its condition holds for the viewport, its properties apply.</summary>
public sealed record MediaRule(MediaCondition Condition, Dictionary<string, IStyleValue> Properties);

/// <summary>
///     <c>min-width = 640px, max-height = 320px</c>: every part must hold. Sizes are in UI units, the
///     viewport the UI is laid out in.
/// </summary>
public sealed class MediaCondition
{
    private readonly List<(string Feature, float Value)> _parts = [];

    public static MediaCondition Parse(string text)
    {
        var condition = new MediaCondition();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length != 2) throw new FormatException($"'{part}' isn't a media feature: write min-width = 640px.");
            var number = pieces[1].EndsWith("px", StringComparison.OrdinalIgnoreCase) ? pieces[1][..^2] : pieces[1];
            var feature = pieces[0];
            if (feature is not ("min-width" or "max-width" or "min-height" or "max-height"))
                throw new FormatException($"Unknown media feature '{feature}': use min-width, max-width, min-height or max-height.");
            condition._parts.Add((feature, float.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture)));
        }

        return condition;
    }

    public bool Holds(float width, float height)
    {
        foreach (var (feature, value) in _parts)
        {
            var holds = feature switch
            {
                "min-width" => width >= value,
                "max-width" => width <= value,
                "min-height" => height >= value,
                _ => height <= value
            };
            if (!holds) return false;
        }

        return true;
    }
}
