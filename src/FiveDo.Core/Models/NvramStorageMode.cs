namespace FiveDo.Core;

/// <summary>
/// Selects how NVRAM is persisted between sessions.
/// </summary>
public enum NvramStorageMode
{
    /// <summary>
    /// One shared NVRAM image for every game. This is the historical 5DO behavior
    /// and the default so existing installations keep their saved data.
    /// </summary>
    Global,

    /// <summary>
    /// A separate NVRAM image per 3DO game, keyed by the 4DO game ID. Opera calls
    /// this mode <c>per game</c>; it prevents one title from overwriting another's
    /// save data.
    /// </summary>
    PerGame,
}
