using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;

namespace ThirtyDollarGUI.Views;

public partial class Greeter : Window
{
    private readonly MainWindow _mainWindow;

    [Obsolete("This constructor is here to remove a single warning. Yes.. I know...")]
    [UsedImplicitly]
    public Greeter()
    {
        throw new Exception("Do not use this constructor please.");
    }

    public Greeter(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        InitializeComponent();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
        _mainWindow.Show();
    }
}