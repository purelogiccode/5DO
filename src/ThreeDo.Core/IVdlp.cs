namespace ThreeDo.Core;

/// <summary>
/// The VDLP display-list processor.
/// </summary>
public interface IVdlp
{
    /// <summary>Sets the head of the display list. FreeDO: <c>_vdl_ProcessVDL(addr)</c>.</summary>
    /// <param name="address">VDL address within VRAM.</param>
    void ProcessVdl(uint address);
}
