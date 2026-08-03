using Avalonia;
using Avalonia.ReactiveUI;
using ThirtyDollarConverter.GUI;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);