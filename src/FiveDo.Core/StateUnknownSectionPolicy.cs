namespace FiveDo.Core;

/// <summary>How the state loader handles sections it does not recognize.</summary>
public enum StateUnknownSectionPolicy
{
    /// <summary>Skip the section, add a warning, and continue loading.</summary>
    Warn,

    /// <summary>Reject the state with a clear error.</summary>
    Reject,
}
