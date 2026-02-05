namespace ThirtyDollarVisualizer.Scenes.Loading.Reports;

public class SampleDownloadReport : IProgressReport
{
    public string Message { get; set; }
    public double Percentage { get; set; }
    
    public string SoundName { get; set; }
    public string DownloadLocation { get; set;  }
}