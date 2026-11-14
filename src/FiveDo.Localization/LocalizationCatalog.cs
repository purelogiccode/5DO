using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace FiveDo.Localization;

/// <summary>
/// A validated locale catalog: a locale code, a native display name, and the translated
/// strings keyed by a stable resource key.
/// </summary>
/// <remarks>
/// Catalogs are stored as embedded JSON files. The <c>en</c> catalog is the baseline and
/// also supplies the reverse lookup used to translate already-written UI text, so a
/// locale can be added without touching every window.
/// </remarks>
public sealed class LocalizationCatalog
{
    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, string> _englishTextToKey;

    private LocalizationCatalog(
        string locale,
        string displayName,
        Dictionary<string, string> strings,
        Dictionary<string, string> englishTextToKey)
    {
        Locale = locale;
        DisplayName = displayName;
        _strings = strings;
        _englishTextToKey = englishTextToKey;
    }

    /// <summary>Gets the normalized locale code, for example <c>en</c> or <c>de</c>.</summary>
    public string Locale { get; }

    /// <summary>Gets the locale name written in that language, for example <c>Deutsch</c>.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the number of translated strings in the catalog.</summary>
    public int Count => _strings.Count;

    /// <summary>Gets a value indicating whether this catalog is the English baseline.</summary>
    public bool IsBaseline => string.Equals(Locale, Localizer.DefaultLocale, StringComparison.Ordinal);

    /// <summary>Gets every key and translation in the catalog.</summary>
    public IReadOnlyDictionary<string, string> Strings => _strings;

    /// <summary>Parses a locale catalog from JSON.</summary>
    /// <param name="json">Catalog JSON.</param>
    /// <param name="sourceName">Source name used in validation errors.</param>
    /// <returns>The parsed catalog.</returns>
    /// <exception cref="InvalidDataException">Thrown when the JSON is malformed or incomplete.</exception>
    public static LocalizationCatalog Parse(string json, string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        using JsonDocument document = ParseDocument(json, sourceName);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Locale catalog '{sourceName}' must be a JSON object.");
        }

        if (!root.TryGetProperty("locale", out JsonElement localeElement)
            || localeElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Locale catalog '{sourceName}' is missing the 'locale' string.");
        }

        var locale = localeElement.GetString()!.Trim();
        if (locale.Length == 0)
        {
            throw new InvalidDataException($"Locale catalog '{sourceName}' has an empty locale code.");
        }

        var displayName = locale;
        if (root.TryGetProperty("displayName", out JsonElement displayElement)
            && displayElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(displayElement.GetString()))
        {
            displayName = displayElement.GetString()!.Trim();
        }

        if (!root.TryGetProperty("strings", out JsonElement stringsElement)
            || stringsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Locale catalog '{sourceName}' is missing the 'strings' object.");
        }

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var englishTextToKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in stringsElement.EnumerateObject())
        {
            var key = property.Name.Trim();
            if (key.Length == 0)
            {
                throw new InvalidDataException($"Locale catalog '{sourceName}' contains an empty key.");
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Locale catalog '{sourceName}' has a non-string value for '{key}'.");
            }

            var value = property.Value.GetString()!;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    $"Locale catalog '{sourceName}' has an empty translation for '{key}'.");
            }

            if (!strings.TryAdd(key, value))
            {
                throw new InvalidDataException(
                    $"Locale catalog '{sourceName}' contains duplicate key '{key}'.");
            }

            if (string.Equals(locale, Localizer.DefaultLocale, StringComparison.Ordinal))
            {
                // First key wins when two resources share the same English text; this keeps
                // the legacy-text lookup deterministic.
                _ = englishTextToKey.TryAdd(value, key);
            }
        }

        if (strings.Count == 0)
        {
            throw new InvalidDataException($"Locale catalog '{sourceName}' has no strings.");
        }

        return new LocalizationCatalog(
            locale.ToLowerInvariant(),
            displayName,
            strings,
            englishTextToKey);
    }

    /// <summary>Looks up a resource key in this catalog.</summary>
    /// <param name="key">Stable resource key.</param>
    /// <returns>The translation, or <see langword="null"/> when the key is absent.</returns>
    public string? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _strings.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Finds the English baseline key for an existing UI string.</summary>
    /// <param name="english">English source text.</param>
    /// <param name="key">Matching resource key.</param>
    /// <returns><see langword="true"/> when a key was found.</returns>
    internal bool TryFindKey(string english, out string key)
    {
        return _englishTextToKey.TryGetValue(english, out key!);
    }

    private static JsonDocument ParseDocument(string json, string sourceName)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Locale catalog '{sourceName}' is not valid JSON.",
                exception);
        }
    }
}

/// <summary>
/// Selects and applies a UI locale. The selected locale is independent of emulation:
/// changing it never touches machine state, input, audio, or frame hashes.
/// </summary>
/// <remarks>
/// The application is built with invariant globalization, so locale selection is an
/// explicit catalog choice rather than a <see cref="CultureInfo"/> change. The catalog is
/// loaded from embedded JSON, which keeps trimming and NativeAOT safe.
/// </remarks>
public static class Localizer
{
    /// <summary>The locale code used for the English baseline catalog.</summary>
    public const string DefaultLocale = "en";

    /// <summary>The setting value that selects the host locale.</summary>
    public const string SystemLocale = "system";

    private static readonly IReadOnlyList<LocalizationCatalog> Catalogs = LoadCatalogs();
    private static readonly LocalizationCatalog EnglishCatalog = FindEnglishCatalog();
    private static LocalizationCatalog _current = FindCatalog(DefaultLocale) ?? EnglishCatalog;

    /// <summary>Gets every available locale, ordered by locale code.</summary>
    public static IReadOnlyList<LocalizationCatalog> Available => Catalogs;

    /// <summary>Gets the active catalog.</summary>
    public static LocalizationCatalog Current => Volatile.Read(ref _current);

    /// <summary>Gets the active locale code.</summary>
    public static string Locale => Current.Locale;

    /// <summary>Gets a value indicating whether a non-English locale is active.</summary>
    public static bool IsTranslated => !Current.IsBaseline;

    /// <summary>Selects a locale, falling back to English when it is unavailable.</summary>
    /// <param name="locale">
    /// A locale code, <see cref="SystemLocale"/>, or <see langword="null"/>/empty for the
    /// system locale.
    /// </param>
    /// <returns>The selected catalog.</returns>
    public static LocalizationCatalog Use(string? locale)
    {
        if (!TryUse(locale) && !TryUse(DefaultLocale))
        {
            Volatile.Write(ref _current, EnglishCatalog);
        }

        return Current;
    }

    /// <summary>Tries to select a locale without changing the current locale on failure.</summary>
    /// <param name="locale">A locale code, <see cref="SystemLocale"/>, or empty for the system locale.</param>
    /// <returns><see langword="true"/> when a matching catalog was selected.</returns>
    public static bool TryUse(string? locale)
    {
        var normalized = NormalizeLocale(locale);
        if (normalized.Length == 0)
        {
            normalized = ResolveSystemLocale();
        }

        var catalog = FindCatalog(normalized);
        if (catalog is null)
        {
            return false;
        }

        Volatile.Write(ref _current, catalog);
        return true;
    }

    /// <summary>
    /// Resolves the host locale from the environment. The desktop setting and
    /// <c>--locale</c> option take precedence over this value.
    /// </summary>
    /// <returns>A supported locale code, or <see cref="DefaultLocale"/> when none matches.</returns>
    public static string ResolveSystemLocale()
    {
        string[] variables = ["FIVEDO_LOCALE", "LANGUAGE", "LC_ALL", "LC_MESSAGES", "LANG"];
        foreach (var variable in variables)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // LANGUAGE may contain a priority list; LC_* may carry an encoding or modifier.
            var first = value.Split(':', 2)[0];
            first = first.Split('.', 2)[0];
            first = first.Split('@', 2)[0];
            var code = NormalizeLocale(first);
            if (code.Length > 0 && FindCatalog(code) is not null)
            {
                return code;
            }
        }

        var ui = NormalizeLocale(CultureInfo.CurrentUICulture.Name);
        return ui.Length > 0 && FindCatalog(ui) is not null ? ui : DefaultLocale;
    }

    /// <summary>Translates a resource key.</summary>
    /// <param name="key">Stable resource key.</param>
    /// <returns>
    /// The active translation, the English baseline when the active locale lacks the key,
    /// or the key itself when no catalog defines it.
    /// </returns>
    public static string Translate(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Current.Get(key)
            ?? EnglishCatalog.Get(key)
            ?? key;
    }

    /// <summary>Translates a resource key and formats its placeholders.</summary>
    /// <param name="key">Stable resource key.</param>
    /// <param name="arguments">Format arguments.</param>
    /// <returns>The formatted translation.</returns>
    public static string Format(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Format(CultureInfo.InvariantCulture, Translate(key), arguments);
    }

    /// <summary>
    /// Translates an existing English UI string by looking it up in the baseline catalog.
    /// Text that is not catalogued is returned unchanged, so untranslated screens still work.
    /// </summary>
    /// <param name="english">English source text.</param>
    /// <returns>The translated text, or the original text when no key exists.</returns>
    public static string TranslateText(string english)
    {
        ArgumentNullException.ThrowIfNull(english);
        LocalizationCatalog current = Current;
        if (current.IsBaseline || english.Length == 0)
        {
            return english;
        }

        if (!EnglishCatalog.TryFindKey(english, out var key))
        {
            return english;
        }

        return current.Get(key) ?? english;
    }

    /// <summary>Normalizes a user-supplied locale code to a lowercase language tag.</summary>
    /// <param name="locale">Locale code such as <c>de</c>, <c>de-DE</c>, or <c>system</c>.</param>
    /// <returns>The normalized code, or an empty string for the system locale.</returns>
    internal static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)
            || string.Equals(locale.Trim(), SystemLocale, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return locale.Trim().Replace('_', '-').ToLowerInvariant();
    }

    private static LocalizationCatalog? FindCatalog(string locale)
    {
        if (locale.Length == 0)
        {
            return null;
        }

        foreach (LocalizationCatalog catalog in Catalogs)
        {
            if (string.Equals(catalog.Locale, locale, StringComparison.Ordinal))
            {
                return catalog;
            }
        }

        // "de-DE" and "de-AT" fall back to the language catalog; an exact regional
        // catalog wins when one is shipped later.
        var separator = locale.IndexOf('-', StringComparison.Ordinal);
        var language = separator > 0 ? locale[..separator] : locale;
        foreach (LocalizationCatalog catalog in Catalogs)
        {
            if (string.Equals(catalog.Locale, language, StringComparison.Ordinal))
            {
                return catalog;
            }
        }

        return null;
    }

    private static LocalizationCatalog FindEnglishCatalog()
    {
        return FindCatalog(DefaultLocale)
            ?? throw new InvalidOperationException("The embedded English locale catalog is missing.");
    }

    private static LocalizationCatalog[] LoadCatalogs()
    {
        var catalogs = new List<LocalizationCatalog>();
        const string prefix = "FiveDo.Localization.Locales.";
        const string suffix = ".json";
        var assembly = typeof(Localizer).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)
                || !resource.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream? stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            catalogs.Add(LocalizationCatalog.Parse(reader.ReadToEnd(), resource));
        }

        if (catalogs.Count == 0)
        {
            throw new InvalidOperationException("No embedded locale catalogs were found.");
        }

        return catalogs
            .OrderBy(catalog => catalog.Locale, StringComparer.Ordinal)
            .ToArray();
    }
}
