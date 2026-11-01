using Avalonia.Media;
using FiveDo.Host.Osd;

namespace FiveDo.Frontend.Avalonia.ViewModels;

/// <summary>
/// Presentation model for one on-screen notification. Brushes are chosen from the
/// shared palette so the XAML template can bind without a converter.
/// </summary>
public sealed class OsdEntry
{
    private static readonly IBrush InfoAccent = new SolidColorBrush(Color.Parse("#3DAEE9"));
    private static readonly IBrush SuccessAccent = new SolidColorBrush(Color.Parse("#57C878"));
    private static readonly IBrush WarningAccent = new SolidColorBrush(Color.Parse("#E0B050"));
    private static readonly IBrush ErrorAccent = new SolidColorBrush(Color.Parse("#E06C6C"));

    private static readonly IBrush InfoBackground = new SolidColorBrush(Color.Parse("#E6141518"));
    private static readonly IBrush SuccessBackground = new SolidColorBrush(Color.Parse("#E615241A"));
    private static readonly IBrush WarningBackground = new SolidColorBrush(Color.Parse("#E6282414"));
    private static readonly IBrush ErrorBackground = new SolidColorBrush(Color.Parse("#E6281518"));

    /// <summary>Initializes a new instance of the <see cref="OsdEntry"/> class.</summary>
    /// <param name="text">Notification text.</param>
    /// <param name="severity">Notification severity.</param>
    public OsdEntry(string text, OsdSeverity severity)
    {
        Text = text;
        Accent = severity switch
        {
            OsdSeverity.Success => SuccessAccent,
            OsdSeverity.Warning => WarningAccent,
            OsdSeverity.Error => ErrorAccent,
            _ => InfoAccent,
        };
        Background = severity switch
        {
            OsdSeverity.Success => SuccessBackground,
            OsdSeverity.Warning => WarningBackground,
            OsdSeverity.Error => ErrorBackground,
            _ => InfoBackground,
        };
    }

    /// <summary>Gets the notification text.</summary>
    public string Text { get; }

    /// <summary>Gets the severity accent brush.</summary>
    public IBrush Accent { get; }

    /// <summary>Gets the panel background brush.</summary>
    public IBrush Background { get; }
}
