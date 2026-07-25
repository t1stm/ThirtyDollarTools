using Avalonia;
using Avalonia.ReactiveUI;
using ThirtyDollarGUI;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);