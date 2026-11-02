namespace FiveDo.Fuzz;

/// <summary>A parser or command-stream target.</summary>
internal interface IFuzzTarget
{
    /// <summary>Gets the stable target name used in reports and reproduction paths.</summary>
    string Name { get; }

    /// <summary>Generates one input without executing it.</summary>
    /// <param name="context">Target context.</param>
    /// <param name="iteration">Zero-based iteration.</param>
    /// <returns>The generated input.</returns>
    FuzzCase CreateCase(FuzzContext context, int iteration);

    /// <summary>Executes one generated input.</summary>
    /// <param name="fuzzCase">Generated input.</param>
    /// <param name="context">Target context.</param>
    void Execute(FuzzCase fuzzCase, FuzzContext context);
}
