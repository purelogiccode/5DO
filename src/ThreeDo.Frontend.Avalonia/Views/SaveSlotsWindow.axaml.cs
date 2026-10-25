using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ThreeDo.Host;

namespace ThreeDo.Frontend.Avalonia.Views;

/// <summary>
/// Lists numbered save slots and lets the user save, load, or delete the selected slot.
/// </summary>
public partial class SaveSlotsWindow : Window
{
    private readonly SaveStateSlotManager _manager;
    private readonly Func<int, SaveStateSlotInfo?> _saveCurrent;
    private readonly Action<int> _load;
    private readonly Action<int> _delete;

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

    private void OnRefresh(object? sender, RoutedEventArgs e) => Refresh();

    private void OnSaveCurrent(object? sender, RoutedEventArgs e)
    {
        var slot = SelectedSlot();
        var info = _saveCurrent(slot);
        if (info is null)
        {
            StatusText.Text = "Load a BIOS and disc before saving.";
            return;
        }

        StatusText.Text = $"Saved slot {info.DisplaySlot}.";
        Refresh();
    }

    private void OnLoad(object? sender, RoutedEventArgs e) => LoadSelected();

    private void OnDoubleTapped(object? sender, TappedEventArgs e) => LoadSelected();

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        var slot = SelectedSlot();
        var info = SlotList.SelectedItem as SaveStateSlotInfo;
        if (info is null || !info.Exists)
        {
            StatusText.Text = $"Slot {slot + 1} is empty.";
            return;
        }

        _delete(slot);
        StatusText.Text = $"Deleted slot {slot + 1}.";
        Refresh();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void LoadSelected()
    {
        var slot = SelectedSlot();
        var info = SlotList.SelectedItem as SaveStateSlotInfo;
        if (info is null || !info.Exists)
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
        var slots = _manager.List();
        SlotList.ItemsSource = slots;
        SlotList.SelectedItem = slots.FirstOrDefault(slot => slot.Slot == _manager.CurrentSlot) ?? slots[0];
        if (slots.All(slot => !slot.Exists))
        {
            StatusText.Text = "No save states in this directory.";
        }
    }
}
