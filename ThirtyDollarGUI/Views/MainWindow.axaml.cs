using Avalonia.Controls;

namespace ThirtyDollarGUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var position = e.ToString();
    }
}