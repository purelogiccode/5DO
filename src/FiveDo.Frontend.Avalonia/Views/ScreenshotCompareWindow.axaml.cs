using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>Shows two screenshots side by side for visual comparison.</summary>
public partial class ScreenshotCompareWindow : Window
{
    private Bitmap? _left;
    private Bitmap? _right;

    /// <summary>Creates an empty comparison window for XAML preview and smoke tests.</summary>
    public ScreenshotCompareWindow()
    {
        InitializeComponent();
        StatusText.Text = "Select two screenshots in the gallery to compare them.";
        Closed += (_, _) => DisposeImages();
    }

    /// <summary>Creates a comparison window for two entries.</summary>
    /// <param name="left">Left screenshot.</param>
    /// <param name="right">Right screenshot.</param>
    public ScreenshotCompareWindow(ScreenshotEntry left, ScreenshotEntry right)
        : this()
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        _left = Load(left);
        _right = Load(right);
        LeftImage.Source = _left;
        RightImage.Source = _right;
        LeftCaption.Text = left.Caption;
        RightCaption.Text = right.Caption;
        StatusText.Text = "Compare the two captures. Close the window to return to the gallery.";
    }

    private static Bitmap? Load(ScreenshotEntry entry)
    {
        try
        {
            using FileStream stream = File.OpenRead(entry.Path);
            return Bitmap.DecodeToWidth(stream, 1600);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Screenshot {Path} could not be decoded for comparison.", entry.Path);
            return null;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void DisposeImages()
    {
        _left?.Dispose();
        _right?.Dispose();
        _left = null;
        _right = null;
    }
}
