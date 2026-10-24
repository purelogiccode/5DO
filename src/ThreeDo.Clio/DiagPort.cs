using ThreeDo.Core;

namespace ThreeDo.Clio;

/// <summary>
/// The diagnostic/test shift port. FreeDO: <c>DiagPort.cpp</c>.
/// </summary>
/// <remarks>
/// Two 16-bit shift registers form a bit-serial link between the ARM and the diagnostics
/// hardware. The port is normally unused by games; it exists for BIOS self-tests.
/// </remarks>
public sealed class DiagPort : IDiagPort
{
    private ushort _receiveFifo0;
    private ushort _receiveFifo1;
    private ushort _getPointer;
    private ushort _sendPointer;

    /// <summary>Initializes a new instance of the <see cref="DiagPort"/> class.</summary>
    public DiagPort()
    {
        Reset(-1);
    }

    /// <summary>Initializes the port. FreeDO: <c>_diag_Init</c>.</summary>
    /// <param name="testCode">Diagnostic test selector; a negative value selects no test.</param>
    public void Reset(int testCode)
    {
        _getPointer = 16;
        _sendPointer = 16;

        if (testCode >= 0)
        {
            testCode ^= 0xFF;
            testCode |= 0xA000;
        }
        else
        {
            testCode = 0;
        }

        _receiveFifo0 = unchecked((ushort)testCode);
        _receiveFifo1 = unchecked((ushort)testCode);
    }

    /// <summary>Sends one diagnostic bit pair. FreeDO: <c>_diag_Send</c>.</summary>
    /// <param name="value">Value to send.</param>
    public void Send(uint value)
    {
        if (_getPointer != 16)
        {
            _getPointer = 16;
            _sendPointer = 16;
        }

        _sendPointer--;
        if (_sendPointer == 0)
        {
            _sendPointer = 16;
        }
    }

    /// <summary>Reads one diagnostic bit pair. FreeDO: <c>_diag_Get</c>.</summary>
    /// <returns>The received value.</returns>
    public uint Get()
    {
        if (_sendPointer != 16)
        {
            _getPointer = 16;
            _sendPointer = 16;
        }

        var value = (uint)((_receiveFifo0 >> (_getPointer - 1)) & 1);
        value |= (uint)(((_receiveFifo1 >> (_getPointer - 1)) & 1) << 1);
        _getPointer--;
        if (_getPointer == 0)
        {
            _getPointer = 16;
        }

        return value;
    }
}
