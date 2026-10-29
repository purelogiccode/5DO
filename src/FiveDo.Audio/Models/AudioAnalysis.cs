using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace FiveDo.Audio;


/// <summary>WAV format information plus peak/RMS/clipping statistics.</summary>
/// <param name="File">File format information.</param>
/// <param name="Statistics">Streaming statistics for the complete data chunk.</param>
public sealed record AudioAnalysis(AudioFileInfo File, AudioStatistics Statistics);
