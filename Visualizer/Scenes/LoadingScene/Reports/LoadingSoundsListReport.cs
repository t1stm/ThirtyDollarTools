namespace LoadingScene.Reports;

public class LoadingSoundsListReport : IProgressReport
{
    public string Message => "Reading the sound list";
    public double Percentage => 0.02;
    public string Detail => "thirtydollar.website";
}
