
namespace FiveDo.Frontend.Avalonia;


/// <summary>CPU/GPU palette parity for a 1:1 Accurate probe capture.</summary>
/// <param name="Compared">Whether a parity comparison was performed.</param>
/// <param name="Matches">Whether every RGB pixel matched the CPU oracle.</param>
/// <param name="DifferentPixels">Number of RGB pixels that differ.</param>
/// <param name="CpuHash">FNV-1a hash of the CPU oracle pixels.</param>
/// <param name="GpuHash">FNV-1a hash of the captured GPU pixels in the matched orientation.</param>
/// <param name="VerticalFlip">Whether the captured rows had to be flipped to match.</param>
public readonly record struct GlProbeParity(
    bool Compared,
    bool Matches,
    int DifferentPixels,
    uint CpuHash,
    uint GpuHash,
    bool VerticalFlip);
