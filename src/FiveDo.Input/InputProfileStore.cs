using System;
using System.IO;
using System.Text.Json;

namespace FiveDo.Input;

/// <summary>
/// Loads and saves <see cref="InputProfile"/> as JSON.
/// </summary>
public static class InputProfileStore
{
    /// <summary>Loads a profile file, creating and saving defaults when it does not exist.</summary>
    /// <param name="path">Profile path.</param>
    /// <returns>The loaded or default profile.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file exists but is not a valid profile.</exception>
    public static InputProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            var defaults = InputProfile.CreateDefault();
            Save(path, defaults);
            return defaults;
        }

        try
        {
            InputProfile? profile = JsonSerializer.Deserialize(File.ReadAllText(path), InputProfileJsonContext.Default.InputProfile);
            if (profile is null)
            {
                throw new InvalidDataException($"Input profile '{path}' is empty.");
            }

            profile.Normalize();
            return profile;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Input profile '{path}' is not valid JSON.", exception);
        }
    }

    /// <summary>Saves a profile atomically.</summary>
    /// <param name="path">Profile path.</param>
    /// <param name="profile">Profile to save.</param>
    public static void Save(string path, InputProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(profile);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(profile, InputProfileJsonContext.Default.InputProfile));
        File.Move(temporary, path, overwrite: true);
    }
}
