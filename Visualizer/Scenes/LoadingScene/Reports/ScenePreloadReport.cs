namespace LoadingScene.Reports;

/// <summary>
///     One scene being built ahead of the download. See <see cref="LoadingScene.Loader" />:
///     the scenes go up first so the frames they cost are spent on the loading screen,
///     where there is something to look at, rather than between the download finishing and
///     the home screen appearing.
/// </summary>
public class ScenePreloadReport : IProgressReport
{
    public string Message { get; init; } = string.Empty;
    public double Percentage { get; init; }
    public string? Detail { get; init; }
}
