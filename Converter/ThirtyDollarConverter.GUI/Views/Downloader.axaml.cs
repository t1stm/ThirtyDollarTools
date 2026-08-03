using Avalonia.Controls;
using Avalonia.Input;

namespace ThirtyDollarConverter.GUI.Views;

public partial class Downloader : Window
{
    public Downloader()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}