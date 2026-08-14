namespace Sundex.MSDF.Fonts;

/// <summary>
///     Font-wide vertical metrics, in <b>raw font units</b> rather than ems. Callers that want
///     ems divide by <see cref="EmSize" />; line spacing is the ratio
///     <c>LineHeight / EmSize</c> (1.2 for Lato).
/// </summary>
public readonly record struct MsdfFontMetrics(
    double EmSize,
    double AscenderY,
    double DescenderY,
    double LineHeight,
    double UnderlineY,
    double UnderlineThickness);
