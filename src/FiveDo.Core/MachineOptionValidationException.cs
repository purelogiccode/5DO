using System;

namespace FiveDo.Core;

/// <summary>
/// Thrown when a machine option value is unknown or outside its supported range.
/// </summary>
/// <remarks>
/// The exception keeps the <see cref="OptionKey"/> so callers can report which registry
/// entry failed instead of parsing a message.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Roslynator",
    "RCS1194",
    Justification = "Standard constructors are present; the typed constructor carries the option key.")]
public sealed class MachineOptionValidationException : ArgumentOutOfRangeException
{
    /// <summary>Initializes a new instance of the <see cref="MachineOptionValidationException"/> class.</summary>
    public MachineOptionValidationException()
    {
        OptionKey = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="MachineOptionValidationException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public MachineOptionValidationException(string message)
        : base(message)
    {
        OptionKey = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="MachineOptionValidationException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public MachineOptionValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        OptionKey = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="MachineOptionValidationException"/> class.</summary>
    /// <param name="optionKey">Registry key of the invalid option.</param>
    /// <param name="actualValue">The rejected value.</param>
    /// <param name="message">A human-readable explanation.</param>
    public MachineOptionValidationException(string optionKey, object? actualValue, string message)
        : base(optionKey, actualValue, message)
    {
        OptionKey = optionKey;
    }

    /// <summary>Gets the registry key of the invalid option.</summary>
    public string OptionKey { get; }
}

/// <summary>
/// Thrown when a machine option key is not present in <see cref="MachineOptionRegistry"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Roslynator",
    "RCS1194",
    Justification = "The option-key constructor is the only meaningful construction path.")]
public sealed class UnknownMachineOptionException : ArgumentException
{
    /// <summary>Initializes a new instance of the <see cref="UnknownMachineOptionException"/> class.</summary>
    public UnknownMachineOptionException()
    {
        OptionKey = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="UnknownMachineOptionException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public UnknownMachineOptionException(string message, Exception innerException)
        : base(message, innerException)
    {
        OptionKey = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="UnknownMachineOptionException"/> class.</summary>
    /// <param name="optionKey">The unknown key.</param>
    public UnknownMachineOptionException(string optionKey)
        : base($"Unknown machine option '{optionKey}'.", nameof(optionKey))
    {
        OptionKey = optionKey;
    }

    /// <summary>Gets the unknown option key.</summary>
    public string OptionKey { get; }
}
