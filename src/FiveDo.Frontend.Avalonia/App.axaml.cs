using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Frontend.Avalonia.ViewModels;
using FiveDo.Frontend.Avalonia.Views;

namespace FiveDo.Frontend.Avalonia;

public class App : Application
{
    private InstanceLock? _instanceLock;

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

                if (!Program.SmokeTest && !InstanceLock.IsBypassed)
                {
                    if (!InstanceLock.TryAcquire(PortablePaths.DataDirectory, out InstanceLock? acquired, out var conflict))
                    {
                        desktop.MainWindow = CreateInstanceConflictWindow(
                            conflict ?? "Another 5DO instance is using this data directory.");
                        desktop.Exit += (_, _) => ReleaseInstanceLock();
                        base.OnFrameworkInitializationCompleted();
                        return;
                    }

                    _instanceLock = acquired;
                    desktop.Exit += (_, _) => ReleaseInstanceLock();
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

    private static Window CreateInstanceConflictWindow(string message)
    {
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };
        ToolTip.SetTip(text, "Close the other 5DO window or start this one with a separate portable folder or --config-profile.");
        var hint = new TextBlock
        {
            Text = "Use --config-profile <name> or FIVEDO_DATA_DIR to run a second isolated instance.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 11,
        };
        ToolTip.SetTip(hint, "Named profiles and explicit data directories use separate lock files.");
        var quit = new Button
        {
            Content = "Quit",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(quit, "Close this window.");
        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
        };
        panel.Children.Add(text);
        panel.Children.Add(hint);
        panel.Children.Add(quit);
        var window = new Window
        {
            Title = "5DO is already running",
            Width = 480,
            Height = 220,
            Content = panel,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        quit.Click += (_, _) => window.Close();
        return window;
    }

    private void ReleaseInstanceLock()
    {
        _instanceLock?.Dispose();
        _instanceLock = null;
    }
}
