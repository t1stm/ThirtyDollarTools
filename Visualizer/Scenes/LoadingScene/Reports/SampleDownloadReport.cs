namespace LoadingScene.Reports;

public class SampleDownloadReport : IProgressReport
{
    public string SoundName { get; set; } = string.Empty;
    public string DownloadLocation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public string? Detail => SoundName;
}
