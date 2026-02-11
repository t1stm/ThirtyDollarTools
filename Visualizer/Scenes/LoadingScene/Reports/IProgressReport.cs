namespace LoadingScene.Reports;

public interface IProgressReport
{
    public string Message { get; }
    public double Percentage { get; }
}