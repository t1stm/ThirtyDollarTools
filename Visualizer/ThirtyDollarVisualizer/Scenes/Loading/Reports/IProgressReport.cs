namespace ThirtyDollarVisualizer.Scenes.Loading.Reports;

public interface IProgressReport
{
    public string Message { get; }
    public double Percentage { get; }
}