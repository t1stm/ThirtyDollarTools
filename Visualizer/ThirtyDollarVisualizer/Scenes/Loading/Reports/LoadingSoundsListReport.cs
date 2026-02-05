namespace ThirtyDollarVisualizer.Scenes.Loading.Reports;

public class LoadingSoundsListReport : IProgressReport
{
    public string Message => "Loading Sounds List from https://thirtydollar.website";
    public double Percentage => 0.5;
}