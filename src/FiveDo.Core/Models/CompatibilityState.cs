namespace FiveDo.Core;

/// <summary>
/// Mutable runtime compatibility flags ported from FreeDOCore's file-scope globals.
/// </summary>
/// <remarks>
/// One instance belongs to one <c>FiveDoMachine</c>. These values are not shared statically.
/// </remarks>
public sealed class CompatibilityState
{
    /// <summary>Gets or sets the active fix-mode bits. FreeDO: <c>fixmode</c>.</summary>
    public FixMode FixMode { get; set; }

    /// <summary>Gets or sets the detected Anvil BIOS type. FreeDO: <c>isanvil</c>.</summary>
    public int IsAnvil { get; set; }

    /// <summary>Gets or sets a value indicating whether the BIOS Anvil workaround is active. FreeDO: <c>biosanvil</c>.</summary>
    public bool BiosAnvil { get; set; }

    /// <summary>Gets or sets the CEL timing heuristic state. FreeDO: <c>speedfixes</c>.</summary>
    public int SpeedFixes { get; set; }

    /// <summary>Gets or sets the CEL frame counter used by the timing heuristics. FreeDO: <c>sf</c>.</summary>
    public int Sf { get; set; } = 5_000_000;

    /// <summary>Gets or sets the CEL scale-delay state. FreeDO: <c>sdf</c>.</summary>
    public int Sdf { get; set; }

    /// <summary>Gets or sets the undocumented CEL flag 11 state. FreeDO: <c>unknownflag11</c>.</summary>
    public int UnknownFlag11 { get; set; }

    /// <summary>Gets or sets the <c>jw</c> compatibility state.</summary>
    public int Jw { get; set; }

    /// <summary>Gets or sets a value indicating whether the one-shot Crash 'n Burn PC jump has occurred. FreeDO: <c>cnbfix</c>.</summary>
    public bool Cnbfix { get; set; }

    /// <summary>Gets or sets a value indicating whether high-resolution CEL rendering is active. FreeDO: <c>HightResMode</c>.</summary>
    public bool HighResolution { get; set; }

    /// <summary>Restores the values used at machine initialization.</summary>
    public void Reset()
    {
        FixMode = FixMode.None;
        IsAnvil = 0;
        BiosAnvil = false;
        SpeedFixes = 0;
        Sf = 5_000_000;
        Sdf = 0;
        UnknownFlag11 = 0;
        Jw = 0;
        Cnbfix = false;
        HighResolution = false;
    }
}
