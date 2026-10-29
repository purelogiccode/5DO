namespace FiveDo.Core;

/// <summary>
/// Receives completed frames from the machine.
/// </summary>
/// <remarks>
/// The frame is reused by the emulation core after the callback returns. A host that needs
/// to retain it must copy the lines or upload them synchronously.
/// </remarks>
public interface IFrameSink
{
    /// <summary>Called when a frame is ready for presentation.</summary>
    /// <param name="frame">The completed frame.</param>
    void OnFrame(VdlFrame frame);

    /// <summary>Called at the line-16 trigger in multitask mode.</summary>
    /// <remarks>The single-threaded v1 path does not call this method.</remarks>
    void OnFrameTrigger()
    {
    }
}
