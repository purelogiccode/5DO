using System.Runtime.InteropServices;

namespace FiveDo.Cpu;

/// <summary>One SWI comment observed by the CPU diagnostic counter.</summary>
/// <param name="Comment">The 24-bit SWI comment.</param>
/// <param name="Count">Number of SWIs executed with that comment.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ArmSwiCommentUsage(uint Comment, long Count);
