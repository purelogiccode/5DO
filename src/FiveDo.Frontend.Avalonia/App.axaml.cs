using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FiveDo.Frontend.Avalonia.ViewModels;
using FiveDo.Frontend.Avalonia.Views;

namespace FiveDo.Frontend.Avalonia;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (Program.GlProbe is not null)
                {
                    var probe = new GlProbeWindow(Program.GlProbe, desktop.Shutdown);
                    desktop.MainWindow = probe;
                    base.OnFrameworkInitializationCompleted();
                    return;
                }

                var window = new MainWindow
                {
                    DataContext = new MainViewModel(),
                };
                desktop.MainWindow = window;

                if (Program.SmokeTest)
                {
                    window.Opened += (_, _) =>
                    {
                        // Let one UI pass run so a trimmed publish exercises the XAML,
                        // video-surface construction, and settings paths before shutting down.
                        DispatcherTimer.RunOnce(
                            () =>
                            {
                                Program.CompleteSmokeTest();
                                desktop.Shutdown();
                            },
                            TimeSpan.FromMilliseconds(750));
                    };
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception exception)
        {
            Serilog.Log.Fatal(exception, "Desktop shell initialization failed.");
            throw;
        }
    }
}
