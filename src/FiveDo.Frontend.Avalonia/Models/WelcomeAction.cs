
namespace FiveDo.Frontend.Avalonia.Views;


/// <summary>The action chosen by the first-run welcome dialog.</summary>
public enum WelcomeAction
{
    /// <summary>Continue without opening a file.</summary>
    Continue,

    /// <summary>Open a BIOS ROM.</summary>
    OpenBios,

    /// <summary>Open a disc image.</summary>
    OpenDisc,
}
