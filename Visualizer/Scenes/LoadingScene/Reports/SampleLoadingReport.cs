namespace LoadingScene.Reports;

public class SampleLoadingReport : IProgressReport
{
    public string Message { get; set; } = string.Empty;
    public double Percentage { get; set; }
}