using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Lists numbered save slots and lets the user save, load, or delete the selected slot.
/// </summary>
public partial class SaveSlotsWindow : Window, IDisposable
{
    private readonly SaveStateSlotManager _manager;
    private readonly Func<int, SaveStateSlotInfo?> _saveCurrent;
    private readonly Action<int> _load;
    private readonly Action<int> _delete;
    private Bitmap? _thumbnail;

    /// <summary>Creates the dialog with the default slot directory.</summary>
    public SaveSlotsWindow()
        : this(
            new SaveStateSlotManager(Path.Combine(AppSettingsStore.DefaultDirectory, "states")),
            _ => null,
            _ => { },
            _ => { })
    {
    }

    /// <summary>Creates the dialog.</summary>
    /// <param name="manager">Slot manager.</param>
    /// <param name="saveCurrent">Saves the running machine to a slot and returns its info, or null when unavailable.</param>
    /// <param name="load">Loads a slot into the running machine.</param>
    /// <param name="delete">Deletes a slot.</param>
    public SaveSlotsWindow(
        SaveStateSlotManager manager,
        Func<int, SaveStateSlotInfo?> saveCurrent,
        Action<int> load,
        Action<int> delete)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(saveCurrent);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(delete);
        InitializeComponent();
        _manager = manager;
        _saveCurrent = saveCurrent;
        _load = load;
        _delete = delete;
        Refresh();
    }

    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateThumbnail();
    }

    private void UpdateThumbnail()
    {
        _thumbnail?.Dispose();
        _thumbnail = null;
        ThumbnailImage.Source = null;

        if (SlotList.SelectedItem is not SaveStateSlotInfo { HasThumbnail: true } info)
        {
            NoThumbnailText.IsVisible = true;
            return;
        }

        try
        {
            _thumbnail = new Bitmap(info.ThumbnailPath);
            ThumbnailImage.Source = _thumbnail;
            NoThumbnailText.IsVisible = false;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Save-slot thumbnail could not be loaded for {Path}.", info.ThumbnailPath);
            NoThumbnailText.IsVisible = true;
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _thumbnail?.Dispose();
        _thumbnail = null;
        GC.SuppressFinalize(this);
    }

    private void OnSaveCurrent(object? sender, RoutedEventArgs e)
    {
        var slot = SelectedSlot();
        SaveStateSlotInfo? info = _saveCurrent(slot);
        if (info is null)
        {
            StatusText.Text = "Load a BIOS and disc before saving.";
            return;
        }

        StatusText.Text = $"Saved slot {info.DisplaySlot}.";
        Refresh();
    }

    private void OnLoad(object? sender, RoutedEventArgs e)
    {
        LoadSelected();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        LoadSelected();
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        var slot = SelectedSlot();
        if (SlotList.SelectedItem is not SaveStateSlotInfo info || !info.Exists)
        {
            StatusText.Text = $"Slot {slot + 1} is empty.";
            return;
        }

        _delete(slot);
        StatusText.Text = $"Deleted slot {slot + 1}.";
        Refresh();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadSelected()
    {
        var slot = SelectedSlot();
        if (SlotList.SelectedItem is not SaveStateSlotInfo info || !info.Exists)
        {
            StatusText.Text = $"Slot {slot + 1} is empty.";
            return;
        }

        _load(slot);
        StatusText.Text = $"Loaded slot {slot + 1}.";
        Refresh();
    }

    private int SelectedSlot()
    {
        if (SlotList.SelectedItem is SaveStateSlotInfo info)
        {
            _manager.Select(info.Slot);
            return info.Slot;
        }

        return _manager.CurrentSlot;
    }

    private void Refresh()
    {
        IReadOnlyList<SaveStateSlotInfo> slots = _manager.List();
        SlotList.ItemsSource = slots;
        SlotList.SelectedItem = slots.FirstOrDefault(slot => slot.Slot == _manager.CurrentSlot) ?? slots[0];
        if (slots.All(slot => !slot.Exists))
        {
            StatusText.Text = "No save states in this directory.";
        }

        UpdateThumbnail();
    }
}
