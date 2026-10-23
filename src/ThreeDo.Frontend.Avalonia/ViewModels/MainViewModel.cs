using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreeDo.Frontend.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "No game loaded.";
}
