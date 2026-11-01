using System.Text.Json.Serialization;

namespace FiveDo.Cheats;

/// <summary>One named cheat in a per-game cheat file.</summary>
public sealed class CheatEntry
{
    /// <summary>Gets or sets the display name. Names are unique within a file.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional description shown by the desktop shell.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the cheat code text.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the cheat is applied while the game runs.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the parse error for an invalid code loaded from disk. The value is not
    /// serialized; the original code is preserved so the user can repair it.
    /// </summary>
    [JsonIgnore]
    public string Error { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the loaded code has a parse error.</summary>
    [JsonIgnore]
    public bool HasError => Error.Length > 0;

    /// <summary>Creates a copy of this entry without its transient parse error.</summary>
    /// <returns>A new entry with the same user-editable fields.</returns>
    public CheatEntry Clone()
    {
        return new CheatEntry
        {
            Name = Name,
            Description = Description,
            Code = Code,
            Enabled = Enabled,
        };
    }
}
