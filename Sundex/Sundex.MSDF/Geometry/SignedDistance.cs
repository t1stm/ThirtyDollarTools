namespace Sundex.MSDF.Geometry;

/// <summary>
///     A signed distance to an edge, paired with the cosine of the angle between the query
///     direction and that edge. The angle breaks ties consistently when two edges come out
///     equidistant.
/// </summary>
internal struct SignedDistance(double distance, double dot)
{
    public double Distance = distance;
    public double Dot = dot;

    /// <summary>
    ///     The value a distance search starts from, further away than any real edge. Note this is
    ///     <b>not</b> <c>default(SignedDistance)</c>: a zero distance would compare as nearer than
    ///     everything, so every selector must start from here explicitly.
    /// </summary>
    public static SignedDistance Initial => new(-double.MaxValue, 0);

    /// <summary>Orders by nearness: smaller magnitude wins, and <see cref="Dot" /> breaks ties.</summary>
    public static bool operator <(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) < Math.Abs(b.Distance) ||
        (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot < b.Dot);

    public static bool operator >(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) > Math.Abs(b.Distance) ||
        (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot > b.Dot);

    public static bool operator <=(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) < Math.Abs(b.Distance) ||
        (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot <= b.Dot);

    public static bool operator >=(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) > Math.Abs(b.Distance) ||
        (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot >= b.Dot);

    public override string ToString() => $"{Distance:R} (dot {Dot:R})";
}
