namespace LoadingScene.Reports;

/// <summary>
///     Progress for one scene being built ahead of the download. See
///     <see cref="LoadingScene.Loader" />, which builds the scenes first so the frames they
///     cost are spent while the loading screen is on.
/// </summary>
public class ScenePreloadReport : IProgressReport
{
    public string Message { get; init; } = string.Empty;
    public double Percentage { get; init; }
    public string? Detail { get; init; }
}
