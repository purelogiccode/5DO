namespace FiveDo.Madam;

/// <summary>
/// A parsed CEL control block header.
/// </summary>
/// <remarks>
/// FreeDO parses these fields inline in <c>Madam.cpp: _madam_HandleCEL</c>. Keeping the
/// header in a small value object makes the parser testable before the rasterizer lands.
/// It is a value type so the per-CCB CEL path does not allocate.
/// </remarks>
public struct CcbHeader
{
    /// <summary>Gets or sets the CCB address.</summary>
    public uint Address { get; set; }

    /// <summary>Gets or sets the raw flags word.</summary>
    public CcbControl Flags { get; set; }

    /// <summary>Gets or sets the address of the next field after the header and preamble.</summary>
    public uint BodyAddress { get; set; }

    /// <summary>Gets or sets the next CCB pointer. FreeDO: <c>NEXTCCB</c>.</summary>
    public uint NextCcb { get; set; }

    /// <summary>Gets or sets the source-data pointer. FreeDO: <c>PDATA</c>.</summary>
    public uint PData { get; set; }

    /// <summary>Gets or sets the source-data cursor after preamble loading.</summary>
    public uint PDataCursor { get; set; }

    /// <summary>Gets or sets the PLUT pointer. FreeDO: <c>PLUTDATA</c>.</summary>
    public uint PlutData { get; set; }

    /// <summary>Gets or sets the first preamble word. FreeDO: <c>PRE0</c>.</summary>
    public uint Pre0 { get; set; }

    /// <summary>Gets or sets the second preamble word. FreeDO: <c>PRE1</c>.</summary>
    public uint Pre1 { get; set; }

    /// <summary>Gets or sets the CEL X origin in 16.16 fixed point. FreeDO: <c>XPOS1616</c>.</summary>
    public int XPosition { get; set; }

    /// <summary>Gets or sets the CEL Y origin in 16.16 fixed point. FreeDO: <c>YPOS1616</c>.</summary>
    public int YPosition { get; set; }

    /// <summary>Gets or sets the horizontal delta. FreeDO: <c>HDX1616</c>.</summary>
    public int Hdx { get; set; }

    /// <summary>Gets or sets the horizontal delta Y. FreeDO: <c>HDY1616</c>.</summary>
    public int Hdy { get; set; }

    /// <summary>Gets or sets the vertical delta X. FreeDO: <c>VDX1616</c>.</summary>
    public int Vdx { get; set; }

    /// <summary>Gets or sets the vertical delta Y. FreeDO: <c>VDY1616</c>.</summary>
    public int Vdy { get; set; }

    /// <summary>Gets or sets the perspective horizontal delta. FreeDO: <c>HDDX1616</c>.</summary>
    public int Hddx { get; set; }

    /// <summary>Gets or sets the perspective vertical delta. FreeDO: <c>HDDY1616</c>.</summary>
    public int Hddy { get; set; }

    /// <summary>Gets or sets the pixel-processor control word. FreeDO: <c>PIXC</c>.</summary>
    public uint PixelControl { get; set; }

    /// <summary>Gets or sets a value indicating that the next pointer is out of range or zero.</summary>
    public bool NextIsLast { get; set; }

    /// <summary>Gets or sets a value indicating that the source pointer is out of range.</summary>
    public bool PDataInvalid { get; set; }

    /// <summary>Gets or sets a value indicating that the PLUT pointer is out of range.</summary>
    public bool PlutDataInvalid { get; set; }

    /// <summary>Gets a value indicating whether this CEL is last in the list.</summary>
    public bool IsLast => (Flags & CcbControl.Last) != 0 || NextIsLast;

    /// <summary>Gets a value indicating whether the CEL is skipped.</summary>
    public bool IsSkipped => (Flags & CcbControl.Skip) != 0;

    /// <summary>Gets a value indicating whether the source data is packed.</summary>
    public bool IsPacked => (Flags & CcbControl.Packed) != 0;

    /// <summary>Gets a value indicating whether the CCB loads a PLUT.</summary>
    public bool LoadsPlut => (Flags & CcbControl.LoadPlut) != 0;

    /// <summary>Gets the pixel-over mode bits. FreeDO: <c>CcbControl &amp; CCB_POVER_MASK</c>.</summary>
    public uint PixelOverMode => (uint)Flags & (uint)CcbControl.PixelOverMask;

    /// <summary>Gets the PLUT offset bits. FreeDO: <c>CcbControl &amp; CCB_PLUTA_MASK</c>.</summary>
    public uint PlutOffset => (uint)Flags & (uint)CcbControl.PlutOffsetMask;
}
