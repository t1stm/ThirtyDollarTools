namespace LoadingScene.Reports;

public interface IProgressReport
{
    public string Message { get; }
    public double Percentage { get; }

    /// <summary>What the current step is working on right now - a sound's id while it downloads.</summary>
    public string? Detail => null;
}
