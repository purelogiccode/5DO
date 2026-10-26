using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ThreeDo.Frontend.Avalonia.ViewModels;
using ThreeDo.Frontend.Avalonia.Views;

namespace ThreeDo.Frontend.Avalonia;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
            desktop.MainWindow = window;

            if (Program.SmokeTest)
            {
                window.Opened += (_, _) =>
                {
                    // Let the first render pass run so a trimmed publish exercises the
                    // XAML, video-surface, and settings paths before shutting down.
                    DispatcherTimer.RunOnce(
                        () =>
                        {
                            Program.CompleteSmokeTest();
                            desktop.Shutdown(0);
                        },
                        TimeSpan.FromMilliseconds(750));
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
