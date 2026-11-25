using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Frontend.Avalonia.Localization;
using FiveDo.Frontend.Avalonia.ViewModels;
using FiveDo.Frontend.Avalonia.Views;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Avalonia application entry point. Loads the XAML theme, acquires the single-instance
/// lock, and creates the main emulator window (or the GPU probe / smoke-test host).
/// </summary>
public class App : Application
{
    private InstanceLock? _instanceLock;
    private int _openGlSurfaceFailures;
    private DateTime _lastOpenGlSurfaceFailureUtc;

    /// <summary>Loads the application XAML resources.</summary>
    public override void Initialize()
    {
        // Every window, including dialogs built in code, gets the selected locale after
        // its logical tree is realized. The pass only changes presentation properties.
        Control.LoadedEvent.AddClassHandler<Window>(
            static (window, _) => UiLocalizer.Apply(window));
        try
        {
            AvaloniaXamlLoader.Load(this);
        }
        catch (Exception exception)
        {
            Serilog.Log.Fatal(exception, "Avalonia XAML resources could not be loaded.");
            throw;
        }
    }

    /// <summary>
    /// Runs after Avalonia initialization: installs the UI exception guard, takes the
    /// single-instance lock, and selects the window that should be shown.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
        {
            // Avalonia's OpenGlControlBase throws from the compositor when the driver
            // cannot allocate the control's framebuffer. That happens outside the
            // surface's OnOpenGlRender callback, so it cannot fall back on its own.
            // Switch to the CPU presenter instead of failing the compositor every frame.
            if (TryHandleOpenGlSurfaceFailure(eventArgs.Exception))
            {
                eventArgs.Handled = true;
                return;
            }

            // Exceptions raised by async void event handlers surface on the dispatcher.
            // Log them and keep the process alive instead of letting it terminate.
            Serilog.Log.Error(eventArgs.Exception, "Unhandled UI thread exception.");
            eventArgs.Handled = true;
        };

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
                desktop.Exit += (_, _) => window.Dispose();

                if (Program.SmokeTest)
                {
                    window.Opened += (_, _) =>
                    {
                        // Let one UI pass run so a trimmed publish exercises the XAML,
                        // video-surface construction, and settings paths before shutting down.
                        DispatcherTimer.RunOnce(
                            () =>
                            {
                                Program.CompleteSmokeTest(
                                    window.WindowState.ToString(),
                                    window.LocalizedFirstMenuHeader);
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

    /// <summary>
    /// Handles the framebuffer failure that Avalonia's <c>OpenGlControlBase</c> raises
    /// from the compositor. The failure happens outside the video surface's render
    /// callback, so the surface cannot fall back on its own. A few transient failures
    /// are tolerated (window resize and driver reallocation); a persistent failure
    /// detaches the GL control and switches the game view to the CPU presenter.
    /// </summary>
    /// <param name="exception">Exception raised by the compositor.</param>
    /// <param name="forceFallback">
    /// When true, detach the GL surface immediately. Used for failures that surfaced as
    /// unobserved tasks, where the compositor never observed the exception and cannot be
    /// expected to retry through its normal path.
    /// </param>
    /// <returns><see langword="true"/> when the exception was handled.</returns>
    private bool TryHandleOpenGlSurfaceFailure(Exception exception, bool forceFallback = false)
    {
        OpenGlException? openGlException = FindOpenGlSurfaceFailure(exception);
        if (openGlException is null)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if ((now - _lastOpenGlSurfaceFailureUtc).TotalSeconds >= 10)
        {
            _openGlSurfaceFailures = 0;
        }

        _lastOpenGlSurfaceFailureUtc = now;
        _openGlSurfaceFailures++;
        Serilog.Log.Debug(
            openGlException,
            "The Avalonia OpenGL surface could not allocate its framebuffer (attempt {Attempt}).",
            _openGlSurfaceFailures);

        if (!forceFallback && _openGlSurfaceFailures < 3)
        {
            // Let the compositor retry: a resize can fail once and recover.
            return true;
        }

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow main }:
                main.DisableOpenGlPresenter(openGlException);
                return true;
            case IClassicDesktopStyleApplicationLifetime { MainWindow: GlProbeWindow probe }:
                probe.FailOpenGl(openGlException);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Detects an Avalonia framebuffer failure that surfaced through an unobserved task
    /// (the compositor render loop) instead of the dispatcher, and routes it to the same
    /// presenter fallback used for UI-thread failures.
    /// </summary>
    /// <param name="exception">Unobserved exception, possibly an <see cref="AggregateException"/>.</param>
    /// <returns><see langword="true"/> when the exception was an OpenGL framebuffer failure.</returns>
    internal static bool TryHandleUnobservedOpenGlSurfaceFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        OpenGlException? openGlException = FindOpenGlSurfaceFailure(exception);
        if (openGlException is null)
        {
            return false;
        }

        // The fallback detaches the GL control, so it must run on the UI thread.
        if (Application.Current is not App app)
        {
            return false;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!app.TryHandleOpenGlSurfaceFailure(openGlException, forceFallback: true))
            {
                Serilog.Log.Error(
                    openGlException,
                    "The unobserved OpenGL framebuffer failure could not be routed to a presenter fallback.");
            }
        });
        return true;
    }

    /// <summary>Finds a nested Avalonia OpenGL framebuffer failure.</summary>
    /// <param name="exception">Exception or aggregate to inspect.</param>
    /// <returns>The matching exception, or <see langword="null"/>.</returns>
    internal static OpenGlException? FindOpenGlSurfaceFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        switch (exception)
        {
            case OpenGlException openGlException
                when openGlException.Message.Contains("OpenGL FBO", StringComparison.Ordinal):
                return openGlException;
            case AggregateException aggregate:
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    if (FindOpenGlSurfaceFailure(inner) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            default:
                return exception.InnerException is { } nested
                    ? FindOpenGlSurfaceFailure(nested)
                    : null;
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
