namespace LoadingScene.Reports;

public class NotStartedReport : IProgressReport
{
    public string Message => "Waiting to start";
    public double Percentage => 0;
}
