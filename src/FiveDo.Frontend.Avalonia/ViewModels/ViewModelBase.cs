using CommunityToolkit.Mvvm.ComponentModel;

namespace FiveDo.Frontend.Avalonia.ViewModels;

/// <summary>
/// Base class for desktop view models. Provides <see cref="ObservableObject"/>
/// change notification to every derived view model.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
