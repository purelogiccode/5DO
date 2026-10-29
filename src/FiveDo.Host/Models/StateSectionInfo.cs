using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Host;


/// <summary>One section in a save-state file.</summary>
/// <param name="Id">Section identifier.</param>
/// <param name="Version">Section format version.</param>
/// <param name="PayloadLength">Payload length in bytes.</param>
public sealed record StateSectionInfo(uint Id, ushort Version, int PayloadLength);
