using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ThirtyDollarGUI.ViewModels;
using ThirtyDollarGUI.Views;

namespace ThirtyDollarGUI;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        var main_window = new MainWindow
        {
            DataContext = new MainWindowViewModel()
        };

        var greeter = new Greeter(main_window);
        desktop.MainWindow = greeter;

        main_window.Closing += (_, _) => { greeter.Close(); };

        base.OnFrameworkInitializationCompleted();
    }
}