using System;

namespace FiveDo.Core;

/// <summary>
/// Geometry derived from a <see cref="MemoryConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Opera keeps VRAM directly above DRAM, so the CPU-visible VRAM base is the configured
/// DRAM size. The high-resolution renderer keeps four VRAM planes, so its backing size is
/// <c>DRAM + (4 * VRAM)</c>.
/// </para>
/// <para>
/// <see cref="MadamRedSysBits"/> mirrors <c>opera_mem_madam_red_sysbits</c>. The value is
/// stored in the MADAM RED SYSBITS register (0x004) at reset and is read-only to software.
/// </para>
/// </remarks>
public sealed record MemoryLayout
{
    private const int Megabyte = 1024 * 1024;
    private const uint RedHardware = 0x20;

    private MemoryLayout(MemoryConfiguration configuration, int dramSize, int vramSize)
    {
        Configuration = configuration;
        DramSize = dramSize;
        VramSize = vramSize;
    }

    /// <summary>Gets the stock 2 MB DRAM / 1 MB VRAM layout.</summary>
    public static MemoryLayout Stock { get; } = For(MemoryConfiguration.Dram2MbVram1Mb);

    /// <summary>Gets the selected configuration.</summary>
    public MemoryConfiguration Configuration { get; }

    /// <summary>Gets the CPU-visible DRAM size in bytes.</summary>
    public int DramSize { get; }

    /// <summary>Gets the CPU-visible VRAM size in bytes.</summary>
    public int VramSize { get; }

    /// <summary>Gets the CPU-visible RAM/VRAM size in bytes.</summary>
    public int RamSize => DramSize + VramSize;

    /// <summary>Gets the byte offset of VRAM within the RAM backing array.</summary>
    public int VramBase => DramSize;

    /// <summary>Gets the VRAM address mask used by VDLP, SPORT, and CEL pointers.</summary>
    public uint VramSizeMask => (uint)(VramSize - 1);

    /// <summary>Gets the size of the four high-resolution VRAM planes in bytes.</summary>
    public int HiresVramSize => VramSize * 4;

    /// <summary>Gets the backing size required by high-resolution rendering in bytes.</summary>
    public int HiresRamSize => DramSize + HiresVramSize;

    /// <summary>Gets the DRAM size in megabytes.</summary>
    public int DramMegabytes => DramSize / Megabyte;

    /// <summary>Gets the VRAM size in megabytes.</summary>
    public int VramMegabytes => VramSize / Megabyte;

    /// <summary>Gets the MADAM RED SYSBITS register value for this layout.</summary>
    public uint MadamRedSysBits => RedHardware | (Configuration switch
    {
        MemoryConfiguration.Dram2MbVram1Mb => 0x01u | (0x01u << 3) | (0x01u << 5),
        MemoryConfiguration.Dram2MbVram2Mb => 0x02u | (0x01u << 3) | (0x01u << 5),
        MemoryConfiguration.Dram4MbVram1Mb => 0x01u | (0x02u << 3),
        MemoryConfiguration.Dram4MbVram2Mb => 0x02u | (0x02u << 3),
        MemoryConfiguration.Dram8MbVram1Mb => 0x01u | (0x02u << 3) | (0x02u << 5),
        MemoryConfiguration.Dram8MbVram2Mb => 0x02u | (0x02u << 3) | (0x02u << 5),
        MemoryConfiguration.Dram14MbVram2Mb => 0x02u | (0x03u << 5),
        MemoryConfiguration.Dram15MbVram1Mb => 0x01u | (0x03u << 5),
        _ => throw new InvalidOperationException(
            $"Unknown memory configuration 0x{(byte)Configuration:X2}."),
    });

    /// <summary>Returns the layout for a configuration.</summary>
    /// <param name="configuration">Memory configuration.</param>
    /// <returns>The derived layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown configuration.</exception>
    public static MemoryLayout For(MemoryConfiguration configuration) => configuration switch
    {
        MemoryConfiguration.Dram2MbVram1Mb => new(configuration, 2 * Megabyte, 1 * Megabyte),
        MemoryConfiguration.Dram2MbVram2Mb => new(configuration, 2 * Megabyte, 2 * Megabyte),
        MemoryConfiguration.Dram4MbVram1Mb => new(configuration, 4 * Megabyte, 1 * Megabyte),
        MemoryConfiguration.Dram4MbVram2Mb => new(configuration, 4 * Megabyte, 2 * Megabyte),
        MemoryConfiguration.Dram8MbVram1Mb => new(configuration, 8 * Megabyte, 1 * Megabyte),
        MemoryConfiguration.Dram8MbVram2Mb => new(configuration, 8 * Megabyte, 2 * Megabyte),
        MemoryConfiguration.Dram14MbVram2Mb => new(configuration, 14 * Megabyte, 2 * Megabyte),
        MemoryConfiguration.Dram15MbVram1Mb => new(configuration, 15 * Megabyte, 1 * Megabyte),
        _ => throw new ArgumentOutOfRangeException(
            nameof(configuration),
            configuration,
            "Unknown memory configuration."),
    };

    /// <summary>Formats a configuration as the Opera two-digit hexadecimal code.</summary>
    /// <param name="configuration">Memory configuration.</param>
    /// <returns>An uppercase two-digit code such as <c>21</c>.</returns>
    public static string FormatCode(MemoryConfiguration configuration) =>
        ((byte)configuration).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Formats a configuration as a human-readable size pair.</summary>
    /// <param name="configuration">Memory configuration.</param>
    /// <returns>Text such as <c>2 MB DRAM / 1 MB VRAM</c>.</returns>
    public static string FormatDescription(MemoryConfiguration configuration)
    {
        MemoryLayout layout = For(configuration);
        return $"{layout.DramMegabytes} MB DRAM / {layout.VramMegabytes} MB VRAM";
    }

    /// <summary>Tries to parse a configuration code or size pair.</summary>
    /// <param name="value">
    /// Two-digit hexadecimal code (<c>21</c>), a <c>DRAM/VRAM</c> pair such as <c>4/2</c>, or a
    /// descriptive form such as <c>4MB/2MB</c>.
    /// </param>
    /// <param name="configuration">The parsed configuration.</param>
    /// <returns><see langword="true"/> when the value is a supported configuration.</returns>
    public static bool TryParse(string? value, out MemoryConfiguration configuration)
    {
        configuration = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length == 2
            && byte.TryParse(
                text,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out var code)
            && Enum.IsDefined((MemoryConfiguration)code))
        {
            configuration = (MemoryConfiguration)code;
            return true;
        }

        var normalized = text.Replace("MB", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace('+', '/');
        var parts = normalized.Split('/');
        if (parts.Length != 2
            || !int.TryParse(
                parts[0],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var dram)
            || !int.TryParse(
                parts[1],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var vram))
        {
            return false;
        }

        foreach (MemoryConfiguration candidate in Enum.GetValues<MemoryConfiguration>())
        {
            MemoryLayout layout = For(candidate);
            if (layout.DramMegabytes == dram && layout.VramMegabytes == vram)
            {
                configuration = candidate;
                return true;
            }
        }

        return false;
    }
}
