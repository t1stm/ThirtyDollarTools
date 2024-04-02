using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ThirtyDollarGUI.Views;

public partial class Greeter : Window
{
    private readonly MainWindow MainWindow;

    [Obsolete("This constructor is here to remove a single warning. Yes.. I know...")]
    public Greeter()
    {
        throw new Exception("Do not use this constructor please.");
    }

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