using System;
using System.Runtime.InteropServices;

namespace FiveDo.Audio;

/// <summary>
/// Registers a thread with the Windows multimedia scheduler so it is scheduled like a
/// real-time audio thread instead of ordinary background work.
/// </summary>
/// <remarks>
/// Real-time audio threads must not be starved by UI, rendering, or emulation work. On
/// Windows the supported way to request that treatment is
/// <c>AvSetMmThreadCharacteristics</c>; without it a fill loop can miss its wake-up under
/// load and the output device underruns, which is heard as crackle and dropouts. On
/// platforms without the API the calls are no-ops and the thread priority is relied on.
/// </remarks>
internal static class MultimediaThread
{
    /// <summary>Registers the current thread. The returned token reverts it on dispose.</summary>
    /// <param name="taskName">Scheduler class, for example <c>Pro Audio</c>.</param>
    /// <returns>A token that reverts the registration.</returns>
    internal static MultimediaThreadToken Begin(string taskName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return default;
        }

        var handle = AvSetMmThreadCharacteristics(taskName, out _);
        return new MultimediaThreadToken(handle);
    }

    /// <summary>Reverts a thread registered by <see cref="Begin"/>.</summary>
    /// <param name="handle">Handle from <see cref="Begin"/>.</param>
    internal static void End(nint handle)
    {
        if (OperatingSystem.IsWindows() && handle != 0)
        {
            _ = AvRevertMmThreadCharacteristics(handle);
        }
    }

    [DllImport("avrt.dll", CharSet = CharSet.Unicode)]
    private static extern nint AvSetMmThreadCharacteristics(string taskName, out int taskIndex);

    [DllImport("avrt.dll")]
    private static extern bool AvRevertMmThreadCharacteristics(nint handle);
}
