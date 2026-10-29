using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace FiveDo.Audio;


/// <summary>Describes the PCM format and length of a WAV file.</summary>
/// <param name="Path">Source path.</param>
/// <param name="SampleRate">Samples per second.</param>
/// <param name="Channels">Channel count (1 or 2).</param>
/// <param name="BitsPerSample">Bits per sample (16).</param>
/// <param name="Frames">Number of sample frames.</param>
/// <param name="DataBytes">Number of PCM bytes in the file.</param>
public sealed record AudioFileInfo(
    string Path,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    long Frames,
    long DataBytes)
{
    /// <summary>Gets the duration in seconds.</summary>
    public double DurationSeconds => SampleRate > 0 ? Frames / (double)SampleRate : 0;
}
