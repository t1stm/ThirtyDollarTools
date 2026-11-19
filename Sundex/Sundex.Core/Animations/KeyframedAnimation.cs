using OpenTK.Mathematics;

namespace Sundex.Core.Animations;

public class KeyframedAnimation : Animation
{
    public KeyframedAnimation(IReadOnlyList<Keyframe> keyframes) : base((int)
        Math.Ceiling(keyframes.Sum(position => position.LengthMs)))
    {
        Keyframes = keyframes;
        Features = AnimationFeature.TransformAdd | AnimationFeature.ScaleMultiply | AnimationFeature.RotationAdd |
                   AnimationFeature.DeltaAlpha | AnimationFeature.ColorValue;
    }

    public IReadOnlyList<Keyframe> Keyframes { get; }

    /// <summary>
    ///     A copy of this animation carrying its own <see cref="Animation.TimingStopwatch" />.
    ///     A stylesheet holds one animation object per rule, so each element matching that
    ///     rule must take an instance rather than share the object and, with it, one clock
    ///     that would play them all in lockstep.
    ///     <see cref="Keyframes" /> is a read-only list of structs and is shared as-is.
    /// </summary>
    public KeyframedAnimation CreateInstance()
    {
        return new KeyframedAnimation(Keyframes)
        {
            Features = Features,
            AffectsChildren = AffectsChildren,
            LoopingMode = LoopingMode
        };
    }

    private (Keyframe Current, Keyframe? Next, float Progress) GetCurrentState()
    {
        var elapsed = (float)TimingStopwatch.ElapsedMilliseconds;
        var totalLength = (float)AnimationLength.TotalMilliseconds;

        var loopStartIndex = -1;
        var loopStartMs = 0f;
        var invertIndex = -1;
        var invertMs = 0f;

        var currentMs = 0f;
        for (var index = 0; index < Keyframes.Count; index++)
        {
            if (Keyframes[index].LoopingMode == AnimationLoopingMode.LoopStart)
            {
                loopStartIndex = index;
                loopStartMs = currentMs;
            }

            if (Keyframes[index].LoopingMode == AnimationLoopingMode.Invert)
            {
                invertIndex = index;
                invertMs = currentMs;
            }

            currentMs += Keyframes[index].LengthMs;
        }

        if (loopStartIndex != -1 && invertIndex != -1 && invertIndex > loopStartIndex)
        {
            var loopDuration = invertMs - loopStartMs;
            if (elapsed >= invertMs)
            {
                var loopElapsed = (elapsed - loopStartMs) % (loopDuration * 2);
                if (loopElapsed > loopDuration)
                    // Playing backwards from Invert to LoopStart
                    elapsed = invertMs - (loopElapsed - loopDuration);
                else
                    // Playing forwards from LoopStart to Invert
                    elapsed = loopStartMs + loopElapsed;
            }
        }
        else if (loopStartIndex != -1 && invertIndex == -1)
        {
            if (elapsed >= totalLength)
            {
                var loopDuration = totalLength - loopStartMs;
                elapsed = loopStartMs + (elapsed - loopStartMs) % loopDuration;
            }
        }
        else if (totalLength > 0)
        {
            switch (LoopingMode)
            {
                case AnimationLoopingMode.ResetToStart:
                    elapsed %= totalLength;
                    break;
                case AnimationLoopingMode.Invert:
                    var loopCount = (int)(elapsed / totalLength);
                    elapsed %= totalLength;
                    if (loopCount % 2 == 1) elapsed = totalLength - elapsed;
                    break;

                case AnimationLoopingMode.None:
                case AnimationLoopingMode.LoopStart:
                default:
                    if (elapsed >= totalLength)
                    {
                        if (Keyframes.Count == 0) return (default, null, 0);
                        if (!TimingStopwatch.IsRunning) return (Keyframes[^1], null, 1f);

                        TimingStopwatch.Stop();
                        CallbackOnFinish?.Invoke();
                        return (Keyframes[^1], null, 1f);
                    }

                    break;
            }
        }

        var currentStart = 0f;

        for (var index = 0; index < Keyframes.Count; index++)
        {
            var data = Keyframes[index];
            if (elapsed < currentStart + data.LengthMs)
            {
                var progress = (elapsed - currentStart) / data.LengthMs;
                progress = SteppingFunctions.Apply(data.SteppingFunction, progress);
                return (data, index + 1 < Keyframes.Count ? Keyframes[index + 1] : null, progress);
            }

            currentStart += data.LengthMs;
        }

        if (Keyframes.Count == 0) return (default, null, 0);

        return (Keyframes[^1], null, 1f);
    }

    public override Vector3 GetTransform_Add(Renderable renderable)
    {
        var (current, next, progress) = GetCurrentState();
        if (next == null) return current.Position;

        if (!current.BezierP1.HasValue) return Vector3.Lerp(current.Position, next.Value.Position, progress);
        return current.BezierP2.HasValue
            ?
            // Cubic Bezier
            CalculateCubicBezier(current.Position, current.BezierP1.Value, current.BezierP2.Value, next.Value.Position,
                progress)
            :
            // Quadratic Bezier
            CalculateQuadraticBezier(current.Position, current.BezierP1.Value, next.Value.Position, progress);
    }

    private static Vector3 CalculateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float progress)
    {
        var inverse = 1 - progress;
        return inverse * inverse * start + 2 * inverse * progress * control + progress * progress * end;
    }

    private static Vector3 CalculateCubicBezier(Vector3 start, Vector3 control1, Vector3 control2, Vector3 end, float progress)
    {
        var inverse = 1 - progress;
        return inverse * inverse * inverse * start + 3 * inverse * inverse * progress * control1 + 3 * inverse * progress * progress * control2 + progress * progress * progress * end;
    }

    public override Vector3 GetScale_Multiply(Renderable renderable)
    {
        var (current, next, progress) = GetCurrentState();
        var currentScale = current.Scale ?? Vector3.One;
        if (next == null) return currentScale;

        var nextScale = next.Value.Scale ?? Vector3.One;
        return Vector3.Lerp(currentScale, nextScale, progress);
    }

    public override Vector3 GetRotation_XYZ(Renderable renderable)
    {
        var (current, next, progress) = GetCurrentState();
        var currentRotation = current.Rotation ?? 0;
        if (next == null) return new Vector3(0, 0, currentRotation);

        var nextRotation = next.Value.Rotation ?? 0;
        return new Vector3(0, 0, MathHelper.Lerp(currentRotation, nextRotation, progress));
    }

    public override float GetAlphaDelta_Value(Renderable renderable)
    {
        var (current, next, progress) = GetCurrentState();
        var currentOpacity = current.Opacity ?? 1f;
        if (next == null) return 1f - currentOpacity;

        var nextOpacity = next.Value.Opacity ?? 1f;
        return 1f - MathHelper.Lerp(currentOpacity, nextOpacity, progress);
    }

    public override Vector4 GetColor_Value(Renderable renderable)
    {
        var (current, next, progress) = GetCurrentState();
        var currentColor = current.Color ?? Vector4.One;
        if (next == null) return currentColor;

        var nextColor = next.Value.Color ?? Vector4.One;
        return Vector4.Lerp(currentColor, nextColor, progress);
    }
}