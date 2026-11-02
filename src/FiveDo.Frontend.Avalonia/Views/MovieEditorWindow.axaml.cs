using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FiveDo.Input;
using FiveDo.Tas;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Loads a 5DO movie and edits the player-1 controller state on individual frames.
/// </summary>
public partial class MovieEditorWindow : Window
{
    private readonly ObservableCollection<MovieFrameRow> _rows = [];
    private readonly (CheckBox Box, ControllerButton Button)[] _buttons;
    private Movie? _movie;
    private string? _path;

    /// <summary>Creates the movie editor.</summary>
    public MovieEditorWindow()
    {
        InitializeComponent();
        FrameList.ItemsSource = _rows;
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
        UpdateStatus();
    }

    private void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(LoadAsync, "loading a movie");
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
            _path = path;
            Refresh(0);
            PathText.Text = path;
            SaveButton.IsEnabled = true;
            StatusText.Text = $"Loaded {movie.FrameCount} frames (start {movie.Header.StartFrame}).";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Movie load failed for {Path}.", path);
            StatusText.Text = $"Could not load the movie: {exception.Message}";
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(SaveAsync, "saving a movie");
    }

    private async Task SaveAsync()
    {
        if (_movie is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save edited movie",
            SuggestedFileName = string.IsNullOrWhiteSpace(_path)
                ? "edited.5domovie"
                : Path.GetFileNameWithoutExtension(_path) + "-edited.5domovie",
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
            MovieFile.Save(path, _movie);
            _path = path;
            PathText.Text = path;
            StatusText.Text = $"Saved {_movie.FrameCount} frames to {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "Movie save failed for {Path}.", path);
            StatusText.Text = $"Could not save the movie: {exception.Message}";
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool selected = FrameList.SelectedItem is MovieFrameRow;
        ApplyButton.IsEnabled = selected;
        InsertButton.IsEnabled = selected;
        DeleteButton.IsEnabled = selected;
        TrimButton.IsEnabled = selected;
        if (FrameList.SelectedItem is MovieFrameRow row)
        {
            SetButtons(row.Player1);
        }
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_movie is null || FrameList.SelectedItem is not MovieFrameRow row)
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

        ControllerState[] controllers = _movie.GetControllers(row.Index);
        controllers[0] = state;
        _movie = MovieEditor.SetFrame(_movie, row.Index, controllers);
        Refresh(row.Index);
        StatusText.Text = $"Frame {row.Index}: {PbusDecoder.FormatButtons(state)}.";
    }

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        if (_movie is null || FrameList.SelectedItem is not MovieFrameRow row)
        {
            return;
        }

        ControllerState[] controllers = _movie.GetControllers(row.Index);
        _movie = MovieEditor.InsertFrame(_movie, row.Index + 1, controllers);
        Refresh(row.Index + 1);
        StatusText.Text = $"Inserted a frame after {row.Index}; {_movie.FrameCount} total.";
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_movie is null || FrameList.SelectedItem is not MovieFrameRow row)
        {
            return;
        }

        _movie = MovieEditor.RemoveFrame(_movie, row.Index);
        Refresh(Math.Min(row.Index, _movie.FrameCount - 1));
        StatusText.Text = $"Removed frame {row.Index}; {_movie.FrameCount} total.";
    }

    private void OnTrimClick(object? sender, RoutedEventArgs e)
    {
        if (_movie is null || FrameList.SelectedItem is not MovieFrameRow row)
        {
            return;
        }

        _movie = MovieEditor.Trim(_movie, row.Index + 1);
        Refresh(row.Index);
        StatusText.Text = $"Trimmed to {_movie.FrameCount} frames.";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void Refresh(int selectedIndex)
    {
        _rows.Clear();
        if (_movie is not null)
        {
            for (var i = 0; i < _movie.FrameCount; i++)
            {
                _rows.Add(new MovieFrameRow(i, _movie.GetControllers(i)[0]));
            }
        }

        if (_rows.Count > 0)
        {
            FrameList.SelectedIndex = Math.Clamp(selectedIndex, 0, _rows.Count - 1);
            FrameList.ScrollIntoView(FrameList.SelectedItem!);
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_rows.Count == 0 && _movie is not null)
        {
            StatusText.Text = "The movie has no frames.";
        }
        else if (_movie is null)
        {
            StatusText.Text = "Open a .5domovie file to edit frames.";
        }

        SaveButton.IsEnabled = _movie is not null;
    }

    private void SetButtons(ControllerState state)
    {
        foreach (var (box, button) in _buttons)
        {
            box.IsChecked = state.IsDown(button);
        }
    }
}

/// <summary>One row in the movie frame list.</summary>
public sealed class MovieFrameRow
{
    /// <summary>Initializes a row.</summary>
    /// <param name="index">Zero-based frame index.</param>
    /// <param name="player1">Player-1 controller state.</param>
    public MovieFrameRow(int index, ControllerState player1)
    {
        Index = index;
        Player1 = player1;
        FrameText = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Caption = player1.IsPressed
            ? PbusDecoder.FormatButtons(player1)
            : "(no buttons)";
    }

    /// <summary>Gets the zero-based frame index.</summary>
    public int Index { get; }

    /// <summary>Gets the player-1 controller state.</summary>
    public ControllerState Player1 { get; }

    /// <summary>Gets the frame index text.</summary>
    public string FrameText { get; }

    /// <summary>Gets the formatted button caption.</summary>
    public string Caption { get; }
}
