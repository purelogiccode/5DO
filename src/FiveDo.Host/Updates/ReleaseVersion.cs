using System;
using System.Globalization;

namespace FiveDo.Host;

/// <summary>
/// A semantic release version parsed from a Git tag such as <c>v1.2.0</c>.
/// </summary>
/// <remarks>
/// The update checker accepts the <c>v</c> prefix that 5DO uses for release tags,
/// optional pre-release identifiers, and build metadata. Build metadata is ignored
/// for ordering, matching SemVer 2.0.0.
/// </remarks>
public readonly struct ReleaseVersion : IComparable<ReleaseVersion>, IEquatable<ReleaseVersion>
{
    private readonly string? _prerelease;

    private ReleaseVersion(int major, int minor, int patch, string? prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _prerelease = prerelease;
    }

    /// <summary>Gets the major version number.</summary>
    public int Major { get; }

    /// <summary>Gets the minor version number.</summary>
    public int Minor { get; }

    /// <summary>Gets the patch version number.</summary>
    public int Patch { get; }

    /// <summary>Gets the pre-release identifier, or <see langword="null"/> for a stable release.</summary>
    public string? Prerelease => _prerelease;

    /// <summary>Gets a value indicating whether this is a pre-release.</summary>
    public bool IsPrerelease => !string.IsNullOrEmpty(_prerelease);

    /// <summary>Parses a release version.</summary>
    /// <param name="text">Tag or version text, with an optional leading <c>v</c>.</param>
    /// <param name="version">Parsed version on success.</param>
    /// <returns><see langword="true"/> when the text is a valid version.</returns>
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var buildIndex = value.IndexOf('+', StringComparison.Ordinal);
        if (buildIndex >= 0)
        {
            value = value[..buildIndex];
        }

        string? prerelease = null;
        var prereleaseIndex = value.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            prerelease = value[(prereleaseIndex + 1)..];
            value = value[..prereleaseIndex];
            if (prerelease.Length == 0)
            {
                return false;
            }
        }

        var components = value.Split('.');
        if (components.Length is < 2 or > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < components.Length; i++)
        {
            if (!int.TryParse(
                    components[i],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out numbers[i])
                || numbers[i] < 0)
            {
                return false;
            }
        }

        version = new ReleaseVersion(numbers[0], numbers[1], numbers[2], prerelease);
        return true;
    }

    /// <summary>Compares two release versions using SemVer precedence.</summary>
    /// <param name="other">Version to compare with.</param>
    /// <returns>A negative value when this version is older, zero when equal, or a positive value when newer.</returns>
    public int CompareTo(ReleaseVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (!IsPrerelease && !other.IsPrerelease)
        {
            return 0;
        }

        if (!IsPrerelease)
        {
            return 1;
        }

        if (!other.IsPrerelease)
        {
            return -1;
        }

        return ComparePrerelease(_prerelease!, other._prerelease!);
    }

    /// <inheritdoc />
    public bool Equals(ReleaseVersion other)
    {
        return Major == other.Major
               && Minor == other.Minor
               && Patch == other.Patch
               && string.Equals(_prerelease, other._prerelease, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ReleaseVersion other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch, _prerelease);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
        return IsPrerelease ? $"{value}-{_prerelease}" : value;
    }

    /// <summary>Determines whether one version is older than another.</summary>
    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether one version is newer than another.</summary>
    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether two versions are equal.</summary>
    public static bool operator ==(ReleaseVersion left, ReleaseVersion right) => left.Equals(right);

    /// <summary>Determines whether two versions differ.</summary>
    public static bool operator !=(ReleaseVersion left, ReleaseVersion right) => !left.Equals(right);

    /// <summary>Determines whether one version is older than or equal to another.</summary>
    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether one version is newer than or equal to another.</summary>
    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;

    private static int ComparePrerelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var count = Math.Max(leftParts.Length, rightParts.Length);
        for (var i = 0; i < count; i++)
        {
            if (i >= leftParts.Length)
            {
                return -1;
            }

            if (i >= rightParts.Length)
            {
                return 1;
            }

            var leftPart = leftParts[i];
            var rightPart = rightParts[i];
            var leftNumeric = int.TryParse(
                leftPart,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var leftNumber);
            var rightNumeric = int.TryParse(
                rightPart,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var rightNumber);
            int result;
            if (leftNumeric && rightNumeric)
            {
                result = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                result = -1;
            }
            else if (rightNumeric)
            {
                result = 1;
            }
            else
            {
                result = string.CompareOrdinal(leftPart, rightPart);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }
}
