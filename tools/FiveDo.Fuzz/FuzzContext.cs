using System;
using System.IO;

namespace FiveDo.Fuzz;

/// <summary>State shared by one target for the duration of a run.</summary>
internal sealed class FuzzContext : IDisposable
{
    private readonly string _workspace;

    /// <summary>Initializes a context with a deterministic random source and a temporary workspace.</summary>
    /// <param name="random">Random source seeded for this target.</param>
    internal FuzzContext(Random random)
    {
        Random = random;
        _workspace = Path.Combine(
            Path.GetTempPath(),
            "fivedo-fuzz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspace);
    }

    /// <summary>Gets the deterministic random source.</summary>
    internal Random Random { get; }

    /// <summary>Gets the temporary workspace for this target.</summary>
    internal string Workspace => _workspace;

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_workspace, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
