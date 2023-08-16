using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ThirtyDollarGUI.Views;

public partial class Greeter : Window
{
    private readonly MainWindow MainWindow;
    
    public Greeter(MainWindow mainWindow)
    {
        MainWindow = mainWindow;
        InitializeComponent();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
        MainWindow.Show();
    }
}