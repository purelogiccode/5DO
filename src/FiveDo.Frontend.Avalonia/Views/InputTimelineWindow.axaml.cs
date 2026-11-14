using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Input;
using FiveDo.Tas;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Loads a movie, shows its player-1 input as a per-button timeline, and applies edits
/// back to a movie.
/// </summary>
public partial class InputTimelineWindow : Window
{
    private readonly (CheckBox Box, ControllerButton Button)[] _buttons;
    private Movie? _movie;
    private InputTimeline? _timeline;
    private string? _moviePath;

    /// <summary>Creates the input timeline window.</summary>
    public InputTimelineWindow()
    {
        InitializeComponent();
        _buttons =
        [
            (AButton, ControllerButton.A),
            (BButton, ControllerButton.B),
            (CButton, ControllerButton.C),
            (PButton, ControllerButton.P),
            (XButton, ControllerButton.X),
            (LButton, ControllerButton.L),
            (RButton, ControllerButton.R),
            (UpButton, ControllerButton.Up),
            (DownButton, ControllerButton.Down),
            (LeftButton, ControllerButton.Left),
            (RightButton, ControllerButton.Right),
        ];
        Strip.FrameSelected += OnStripFrameSelected;
        UpdateStatus();
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(LoadAsync, "loading a movie timeline");
    }

    private async Task LoadAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open 5DO movie",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("5DO TAS movie") { Patterns = ["*.5domovie"] },
            ],
        }).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Movie movie = MovieFile.Load(path);
            _movie = movie;
            _moviePath = path;
            _timeline = InputTimeline.FromMovie(movie);
            PathText.Text = path;
            FrameSlider.Maximum = Math.Max(1, movie.FrameCount - 1);
            FrameSlider.Value = 0;
            Strip.Timeline = _timeline;
            Strip.SelectedFrame = 0;
            SelectFrame(0);
            SaveTimelineButton.IsEnabled = true;
            ApplyMovieButton.IsEnabled = true;
            StatusText.Text = $"Loaded {movie.FrameCount} frames and {_timeline.Entries.Count} timeline entries.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Movie timeline load failed for {Path}.", path);
            StatusText.Text = $"Could not load the movie: {exception.Message}";
        }
    }

    private void OnFrameChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        SelectFrame((int)Math.Round(FrameSlider.Value));
    }

    private void OnStripFrameSelected(object? sender, FrameSelectedEventArgs e)
    {
        FrameSlider.Value = Math.Clamp(e.Frame, 0, Math.Max(0, (int)FrameSlider.Maximum));
        SelectFrame(e.Frame);
    }

    private void SelectFrame(int frame)
    {
        if (_timeline is null || _timeline.FrameCount == 0)
        {
            ApplyButton.IsEnabled = false;
            return;
        }

        var selected = Math.Clamp(frame, 0, _timeline.FrameCount - 1);
        Strip.SelectedFrame = selected;
        FrameText.Text = $"{selected} / {_timeline.FrameCount - 1}";
        SetButtons(_timeline.StateAt(selected));
        ApplyButton.IsEnabled = true;
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_timeline is null || _timeline.FrameCount == 0)
        {
            return;
        }

        ControllerState state = ControllerState.Empty;
        foreach (var (box, button) in _buttons)
        {
            if (box.IsChecked == true)
            {
                state = state.With(button);
            }
        }

        var frame = Math.Clamp((int)Math.Round(FrameSlider.Value), 0, _timeline.FrameCount - 1);
        _timeline = _timeline.WithFrame(frame, state);
        Strip.Timeline = _timeline;
        Strip.SelectedFrame = frame;
        StatusText.Text = $"Frame {frame}: {PbusDecoder.FormatButtons(state)}.";
    }

    private void OnSaveTimelineClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(SaveTimelineAsync, "saving an input timeline");
    }

    private async Task SaveTimelineAsync()
    {
        if (_timeline is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save input timeline",
            SuggestedFileName = string.IsNullOrWhiteSpace(_moviePath)
                ? "input.timeline.txt"
                : Path.GetFileNameWithoutExtension(_moviePath) + ".timeline.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Input timeline") { Patterns = ["*.txt"] },
            ],
        }).ConfigureAwait(true);
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _timeline.Save(path);
            StatusText.Text = $"Saved {_timeline.Entries.Count} timeline entries to {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = $"Could not save the timeline: {exception.Message}";
        }
    }

    private void OnApplyMovieClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(ApplyMovieAsync, "applying an input timeline");
    }

    private async Task ApplyMovieAsync()
    {
        if (_timeline is null || _movie is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Apply timeline to movie",
            SuggestedFileName = string.IsNullOrWhiteSpace(_moviePath)
                ? "timeline.5domovie"
                : Path.GetFileNameWithoutExtension(_moviePath) + "-timeline.5domovie",
            DefaultExtension = "5domovie",
            FileTypeChoices =
            [
                new FilePickerFileType("5DO TAS movie") { Patterns = ["*.5domovie"] },
            ],
        }).ConfigureAwait(true);
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Movie updated = _timeline.ApplyTo(_movie);
            MovieFile.Save(path, updated);
            _movie = updated;
            _moviePath = path;
            PathText.Text = path;
            StatusText.Text = $"Applied the timeline and saved {updated.FrameCount} frames to {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = $"Could not apply the timeline: {exception.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void UpdateStatus()
    {
        SaveTimelineButton.IsEnabled = _timeline is not null;
        ApplyMovieButton.IsEnabled = _timeline is not null;
    }

    private void SetButtons(ControllerState state)
    {
        foreach (var (box, button) in _buttons)
        {
            box.IsChecked = state.IsDown(button);
        }
    }
}
