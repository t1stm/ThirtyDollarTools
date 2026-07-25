namespace LoadingScene.Reports;

public class NotStartedReport : IProgressReport
{
    public string Message => "Not Started";
    public double Percentage => 0;
}