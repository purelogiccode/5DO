namespace FiveDo.Core;

/// <summary>
/// A named, versioned region of emulated state.
/// </summary>
public interface IStateSection
{
    /// <summary>Gets the stable section identifier.</summary>
    uint Id { get; }

    /// <summary>Gets the section format version.</summary>
    ushort Version { get; }

    /// <summary>Writes the section payload.</summary>
    /// <param name="writer">State writer.</param>
    void Save(IStateWriter writer);

    /// <summary>Reads the section payload.</summary>
    /// <param name="reader">State reader bounded to this section.</param>
    void Load(IStateReader reader);
}
