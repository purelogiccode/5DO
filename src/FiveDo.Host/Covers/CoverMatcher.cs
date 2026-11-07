using System;
using System.Collections.Generic;
using System.Text;

namespace FiveDo.Host;

/// <summary>
/// Normalizes cover and game names and compares them with exact, token, and
/// Jaro-Winkler matching. The annotation-stripping and Jaro-Winkler behavior mirrors
/// the Simple Launcher cover matcher, extended for Redump/No-Intro/4DO naming.
/// </summary>
public static class CoverMatcher
{
    private const double PrefixScale = 0.1;
    private const int MaxPrefixLength = 4;

    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.Ordinal)
    {
        "a",
        "an",
        "the",
    };

    private static readonly HashSet<string> EditionTokens = new(StringComparer.Ordinal)
    {
        "alpha",
        "beta",
        "build",
        "demo",
        "kiosk",
        "preview",
        "promo",
        "proto",
        "prototype",
        "rental",
        "rev",
        "revision",
        "sample",
        "unl",
        "ver",
        "version",
    };

    private static readonly Dictionary<string, int> RomanNumerals = new(StringComparer.Ordinal)
    {
        ["i"] = 1,
        ["ii"] = 2,
        ["iii"] = 3,
        ["iv"] = 4,
        ["vi"] = 6,
        ["vii"] = 7,
        ["viii"] = 8,
        ["ix"] = 9,
    };

    /// <summary>
    /// Removes balanced parenthetical, square-bracket, and curly-brace groups from a name.
    /// </summary>
    /// <param name="name">Cover or game name.</param>
    /// <returns>The name with annotation groups removed.</returns>
    public static string StripAnnotations(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var result = RemoveBalancedBracketGroups(name, '(', ')');
        result = RemoveBalancedBracketGroups(result, '[', ']');
        result = RemoveBalancedBracketGroups(result, '{', '}');
        result = result.Trim().TrimEnd('.', '_', ' ');
        return string.IsNullOrWhiteSpace(result) ? name : result;
    }

    /// <summary>
    /// Builds the canonical matching form for a cover or game name.
    /// </summary>
    /// <param name="name">Raw name.</param>
    /// <returns>
    /// A lowercase, punctuation-free form with annotations and leading articles removed.
    /// </returns>
    public static string Normalize(string name)
    {
        return Normalize(name, stripAnnotations: true);
    }

    /// <summary>
    /// Builds the canonical matching form for a cover or game name.
    /// </summary>
    /// <param name="name">Raw name.</param>
    /// <param name="stripAnnotations">Whether balanced annotation groups are removed.</param>
    /// <returns>A lowercase, punctuation-free form with leading articles removed.</returns>
    public static string Normalize(string name, bool stripAnnotations)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var stripped = stripAnnotations ? StripAnnotations(name) : name;
        var builder = new StringBuilder(stripped.Length);
        foreach (char raw in stripped)
        {
            char character = NormalizeFullWidth(raw);
            if (character == '&')
            {
                builder.Append(" and ");
                continue;
            }

            if (character is '\'' or '\u2019')
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        var normalized = builder.ToString().Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var significant = new List<string>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!IgnoredTokens.Contains(token))
            {
                significant.Add(token);
            }
        }

        return significant.Count == 0 ? normalized : string.Join(' ', significant);
    }

    /// <summary>Builds the sorted-token form used for order-insensitive exact matching.</summary>
    /// <param name="normalized">Normalized name.</param>
    /// <returns>Space-separated sorted tokens.</returns>
    public static string BuildTokenKey(string normalized)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(tokens, StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }

    /// <summary>Builds the no-space form used for compound-word matching.</summary>
    /// <param name="normalized">Normalized name.</param>
    /// <returns>The normalized name with spaces removed.</returns>
    public static string BuildNoSpaceKey(string normalized)
    {
        return string.IsNullOrEmpty(normalized)
            ? string.Empty
            : normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Calculates the Jaro-Winkler similarity between two strings.</summary>
    /// <param name="left">First string.</param>
    /// <param name="right">Second string.</param>
    /// <returns>A similarity from 0.0 (no match) to 1.0 (identical).</returns>
    public static double CalculateJaroWinklerSimilarity(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return string.IsNullOrEmpty(left) == string.IsNullOrEmpty(right) ? 1.0 : 0.0;
        }

        left = left.ToLowerInvariant();
        right = right.ToLowerInvariant();

        var leftLength = left.Length;
        var rightLength = right.Length;
        var matchDistance = Math.Max(0, Math.Max(leftLength, rightLength) / 2 - 1);
        var matches = 0;
        var leftMatches = new bool[leftLength];
        var rightMatches = new bool[rightLength];

        for (var i = 0; i < leftLength; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(rightLength - 1, i + matchDistance);
            for (var j = start; j <= end; j++)
            {
                if (rightMatches[j] || left[i] != right[j])
                {
                    continue;
                }

                leftMatches[i] = true;
                rightMatches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0)
        {
            return 0.0;
        }

        var k = 0;
        var transpositions = 0;
        for (var i = 0; i < leftLength; i++)
        {
            if (!leftMatches[i])
            {
                continue;
            }

            while (!rightMatches[k])
            {
                k++;
            }

            if (left[i] != right[k])
            {
                transpositions++;
            }

            k++;
        }

        var jaro = ((double)matches / leftLength
                    + (double)matches / rightLength
                    + (matches - transpositions / 2.0) / matches) / 3.0;
        var prefix = 0;
        for (var i = 0; i < Math.Min(MaxPrefixLength, Math.Min(leftLength, rightLength)); i++)
        {
            if (left[i] != right[i])
            {
                break;
            }

            prefix++;
        }

        return jaro + prefix * PrefixScale * (1.0 - jaro);
    }

    /// <summary>Compares two cover or game names using the full matching ladder.</summary>
    /// <param name="left">First name.</param>
    /// <param name="right">Second name.</param>
    /// <returns>A score from 0.0 to 1.0.</returns>
    public static double Score(string? left, string? right)
    {
        var leftName = Normalize(left ?? string.Empty);
        var rightName = Normalize(right ?? string.Empty);
        if (leftName.Length == 0 || rightName.Length == 0)
        {
            return leftName.Length == 0 && rightName.Length == 0 ? 1.0 : 0.0;
        }

        if (string.Equals(leftName, rightName, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (string.Equals(BuildTokenKey(leftName), BuildTokenKey(rightName), StringComparison.Ordinal))
        {
            return 0.99;
        }

        var leftNoSpace = BuildNoSpaceKey(leftName);
        if (leftNoSpace.Length >= 6
            && string.Equals(leftNoSpace, BuildNoSpaceKey(rightName), StringComparison.Ordinal))
        {
            return 0.98;
        }

        var best = CalculateJaroWinklerSimilarity(leftName, rightName);
        var leftAliases = ExpandAliases(left ?? string.Empty);
        foreach (var alias in leftAliases)
        {
            var normalizedAlias = Normalize(alias);
            if (normalizedAlias.Length >= 4)
            {
                best = Math.Max(best, CalculateJaroWinklerSimilarity(normalizedAlias, rightName));
            }
        }

        var rightAliases = ExpandAliases(right ?? string.Empty);
        foreach (var alias in rightAliases)
        {
            var normalizedAlias = Normalize(alias);
            if (normalizedAlias.Length >= 4)
            {
                best = Math.Max(best, CalculateJaroWinklerSimilarity(leftName, normalizedAlias));
            }
        }

        return HasNumberConflict(leftName, rightName) ? Math.Min(best, 0.84) : best;
    }

    /// <summary>
    /// Returns whether two normalized names carry different sequel numbers or Roman numerals.
    /// </summary>
    /// <param name="left">First normalized name.</param>
    /// <param name="right">Second normalized name.</param>
    /// <returns><see langword="true"/> when the numeric token sets differ.</returns>
    public static bool HasNumberConflict(string left, string right)
    {
        if (HasEditionToken(left) || HasEditionToken(right))
        {
            return false;
        }

        HashSet<int> leftNumbers = ExtractNumbers(left);
        HashSet<int> rightNumbers = ExtractNumbers(right);
        if (leftNumbers.Count == 0 && rightNumbers.Count == 0)
        {
            return false;
        }

        return !leftNumbers.SetEquals(rightNumbers);
    }

    /// <summary>
    /// Expands a name into full and subtitle aliases. For example,
    /// <c>A - B - C</c> produces the full name, <c>B - C</c>, and <c>C</c>.
    /// </summary>
    /// <param name="name">Raw name.</param>
    /// <returns>Distinct aliases, preserving the original spelling.</returns>
    public static IReadOnlyList<string> ExpandAliases(string name)
    {
        return ExpandAliases(name, stripAnnotations: true);
    }

    /// <summary>
    /// Expands a name into full and subtitle aliases, optionally keeping annotation groups.
    /// </summary>
    /// <param name="name">Raw name.</param>
    /// <param name="stripAnnotations">Whether balanced annotation groups are removed.</param>
    /// <returns>Distinct aliases, preserving the original spelling.</returns>
    public static IReadOnlyList<string> ExpandAliases(string name, bool stripAnnotations)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var aliases = new List<string>(4);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(name);

        var stripped = stripAnnotations ? StripAnnotations(name) : name;
        Add(stripped);

        var parts = stripped.Split(
            [" - ", " \u2013 ", " \u2014 "],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 1; i < parts.Length; i++)
        {
            Add(string.Join(" - ", parts[i..]));
        }

        return aliases;

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value.Trim()))
            {
                aliases.Add(value.Trim());
            }
        }
    }

    /// <summary>Builds the precomputed keys for one alias.</summary>
    /// <param name="alias">Raw alias.</param>
    /// <param name="stripAnnotations">Whether balanced annotation groups are removed.</param>
    /// <returns>Normalized, token, no-space, and expanded forms.</returns>
    internal static CoverNameKeys BuildKeys(string alias, bool stripAnnotations = true)
    {
        var normalizedAliases = new List<string>(4);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var expanded = ExpandAliases(alias, stripAnnotations);
        foreach (var value in expanded)
        {
            var normalized = Normalize(value, stripAnnotations);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                normalizedAliases.Add(normalized);
            }
        }

        var primary = Normalize(alias, stripAnnotations);
        return new CoverNameKeys(
            alias,
            primary,
            BuildTokenKey(primary),
            BuildNoSpaceKey(primary),
            normalizedAliases);
    }

    private static string RemoveBalancedBracketGroups(string input, char open, char close)
    {
        var result = new StringBuilder(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            if (input[i] == open)
            {
                var depth = 1;
                var j = i + 1;
                while (j < input.Length && depth > 0)
                {
                    if (input[j] == open)
                    {
                        depth++;
                    }
                    else if (input[j] == close)
                    {
                        depth--;
                    }

                    j++;
                }

                if (depth == 0)
                {
                    while (result.Length > 0 && (result[^1] == ' ' || result[^1] == '\t'))
                    {
                        result.Length--;
                    }

                    i = j;
                    continue;
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool HasEditionToken(string normalized)
    {
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (EditionTokens.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<int> ExtractNumbers(string normalized)
    {
        var result = new HashSet<int>();
        if (normalized.Length == 0)
        {
            return result;
        }

        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(
                    token,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var number))
            {
                result.Add(number);
            }
            else if (RomanNumerals.TryGetValue(token, out var roman))
            {
                result.Add(roman);
            }
        }

        return result;
    }

    private static char NormalizeFullWidth(char character)
    {
        if (character is >= '\uFF01' and <= '\uFF5E')
        {
            return (char)(character - 0xFEE0);
        }

        return character == '\u3000' ? ' ' : character;
    }
}

/// <summary>Precomputed matching keys for one cover alias.</summary>
/// <param name="Alias">Original alias text.</param>
/// <param name="Normalized">Canonical normalized form.</param>
/// <param name="TokenKey">Sorted-token form.</param>
/// <param name="NoSpaceKey">Space-free form.</param>
/// <param name="NormalizedAliases">Normalized full and subtitle aliases.</param>
internal sealed record CoverNameKeys(
    string Alias,
    string Normalized,
    string TokenKey,
    string NoSpaceKey,
    IReadOnlyList<string> NormalizedAliases);
