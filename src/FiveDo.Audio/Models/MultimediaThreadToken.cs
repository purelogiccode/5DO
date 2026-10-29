using System;

namespace FiveDo.Audio;

/// <summary>Opaque handle for a thread registered with the Windows multimedia scheduler.</summary>
internal readonly struct MultimediaThreadToken : IDisposable
{
    private readonly nint _handle;

    internal MultimediaThreadToken(nint handle) => _handle = handle;

    /// <summary>Reverts the thread to normal scheduling.</summary>
    public void Dispose()
    {
        if (_handle != 0)
        {
            MultimediaThread.End(_handle);
        }
    }
}
