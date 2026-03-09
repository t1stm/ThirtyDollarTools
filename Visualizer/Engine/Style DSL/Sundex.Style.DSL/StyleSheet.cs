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
                steppingFunctionValue is StringValue sv)
                globalSteppingFunction = SteppingFunctions.ParseSteppingFunction(sv.Value);

            var globalLength = 0;
            if (values.TryGetValue("length", out var lengthValue) && lengthValue is NumberValue lv)
                globalLength = lv.Unit switch
                {
                    "ms" => (int)lv.Value,
                    "s" => (int)(lv.Value * 1000),
                    "m" => (int)(lv.Value * 60000),
                    _ => throw new ArgumentException($"Invalid length unit {lv.Unit}")
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
            var loopingMode = values.TryGetValue("looping-mode", out var loopingModeValue)
                ? (StringValue)loopingModeValue
                : null;

            if (loopingMode is not null && Enum.TryParse<AnimationLoopingMode>(loopingMode.Value, out var loopMode))
                keyframed.LoopingMode = loopMode;

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
                keyframe.Opacity = numberValue.Value;
                break;
            }

            case "color" when value is ColorValue colorValue:
            {
                keyframe.Color = colorValue.Vector;
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
        var key = $"state[{state}]";

        var ids = IDTags.GetAlternateLookup<ReadOnlySpan<char>>();
        var classes = Classes.GetAlternateLookup<ReadOnlySpan<char>>();
        var components = Components.GetAlternateLookup<ReadOnlySpan<char>>();

        if (ids.TryGetValue(name, out var idProps) && idProps.TryGetValue(key, out var idState) &&
            idState is BlockValue idBlock) return idBlock.Properties;
        if (classes.TryGetValue(name, out var classProps) && classProps.TryGetValue(key, out var classState) &&
            classState is BlockValue classBlock) return classBlock.Properties;
        if (components.TryGetValue(name, out var componentProps) &&
            componentProps.TryGetValue(key, out var componentState) &&
            componentState is BlockValue componentBlock) return componentBlock.Properties;
        return null;
    }
}