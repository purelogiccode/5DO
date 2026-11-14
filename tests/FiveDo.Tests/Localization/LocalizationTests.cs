using System;
using System.IO;
using System.Linq;
using FiveDo.Localization;

namespace FiveDo.Tests.Localization;

/// <summary>
/// The localizer holds process-wide state, so these tests must not run alongside each
/// other. Every test restores the active locale in a finally block as well.
/// </summary>
[CollectionDefinition("Localizer", DisableParallelization = true)]
public sealed class LocalizerTestsGroup
{
}

[Collection("Localizer")]
public sealed class LocalizationTests
{
    [Fact]
    public void EmbeddedCatalogs_IncludeEveryShippedLocale()
    {
        string[] locales = ["de", "en", "es", "fr", "it", "ja", "ko", "pt-br", "zh"];
        foreach (var locale in locales)
        {
            LocalizationCatalog catalog = GetCatalog(locale);
            Assert.Equal(locale, catalog.Locale);
            Assert.False(string.IsNullOrWhiteSpace(catalog.DisplayName));
            Assert.True(catalog.Count > 400);
        }

        Assert.Equal("Deutsch", GetCatalog("de").DisplayName);
        Assert.Equal("English", GetCatalog("en").DisplayName);
        Assert.Equal("Português (Brasil)", GetCatalog("pt-br").DisplayName);
        Assert.Equal("日本語", GetCatalog("ja").DisplayName);
        Assert.Equal("한국어", GetCatalog("ko").DisplayName);
        Assert.Equal("简体中文", GetCatalog("zh").DisplayName);
    }

    [Fact]
    public void EveryTranslatedLocale_CoversEveryNonToolTipString()
    {
        LocalizationCatalog english = GetCatalog("en");
        foreach (LocalizationCatalog catalog in Localizer.Available)
        {
            if (catalog.IsBaseline)
            {
                continue;
            }

            var missing = english.Strings.Keys
                .Where(key => !key.StartsWith("ui.tip.", StringComparison.Ordinal))
                .Where(key => !catalog.Strings.ContainsKey(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                missing.Length == 0,
                $"Locale '{catalog.Locale}' is missing non-tooltip keys:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, missing));
            Assert.All(catalog.Strings, pair =>
            {
                Assert.True(english.Strings.ContainsKey(pair.Key));
                Assert.False(string.IsNullOrWhiteSpace(pair.Value));
            });
        }
    }

    [Fact]
    public void SwitchingLocales_RetranslatesAndRestoresEnglish()
    {
        using var locale = new ActiveLocale("de");
        Assert.Equal("Datei", Localizer.TranslateText("File"));

        Assert.True(Localizer.TryUse("fr"));
        Assert.Equal("Fichier", Localizer.TranslateText("File"));

        Assert.True(Localizer.TryUse(Localizer.DefaultLocale));
        Assert.Equal("File", Localizer.TranslateText("File"));
    }

    [Fact]
    public void GermanLocale_TranslatesTheCoreShellToolTips()
    {
        using var locale = new ActiveLocale("de");
        string[] shellToolTips =
        [
            "Boot a disc image",
            "Open a 3DO BIOS ROM",
            "Start or pause emulation",
            "Save state",
            "Load state",
            "Toggle fullscreen",
            "Configure the emulator",
            "Return to the game library.",
            "Show or hide the log dock at the bottom of the window.",
        ];

        foreach (var text in shellToolTips)
        {
            var translated = Localizer.TranslateText(text);
            Assert.False(
                string.Equals(text, translated, StringComparison.Ordinal),
                $"'{text}' was not translated.");
        }
    }

    [Fact]
    public void MenuText_TranslatesThroughTheBaseline()
    {
        using var locale = new ActiveLocale("de");
        Assert.Equal("Datei", Localizer.TranslateText("File"));
        Assert.Equal("Einstellungen...", Localizer.TranslateText("Settings..."));
        Assert.Equal("Datei", Localizer.Translate("ui.file"));
        Assert.Equal("Unbekannter Befehl: demo", Localizer.Format("cli.unknown_command", "demo"));
    }

    [Fact]
    public void UnknownText_IsReturnedUnchanged()
    {
        using var locale = new ActiveLocale("de");
        Assert.Equal("A game-specific status line", Localizer.TranslateText("A game-specific status line"));
        Assert.Equal("missing.key", Localizer.Translate("missing.key"));
    }

    [Fact]
    public void EnglishLocale_ReturnsSourceTextUnchanged()
    {
        using var locale = new ActiveLocale("en");
        Assert.False(Localizer.IsTranslated);
        Assert.Equal("File", Localizer.TranslateText("File"));
        Assert.Equal("File", Localizer.Translate("ui.file"));
    }

    [Fact]
    public void RegionAndCaseVariants_ResolveToTheLanguageCatalog()
    {
        using var locale = new ActiveLocale("en");
        Assert.True(Localizer.TryUse("DE-de"));
        Assert.Equal("de", Localizer.Locale);
        Assert.True(Localizer.TryUse("de_AT"));
        Assert.Equal("de", Localizer.Locale);
        Assert.True(Localizer.TryUse("PT"));
        Assert.Equal("pt-br", Localizer.Locale);
        Assert.True(Localizer.TryUse("pt-BR"));
        Assert.Equal("pt-br", Localizer.Locale);
        Assert.True(Localizer.TryUse("pt-PT"));
        Assert.Equal("pt-br", Localizer.Locale);
        Assert.False(Localizer.TryUse("zz-ZZ"));
        Assert.Equal("pt-br", Localizer.Locale);
    }

    [Fact]
    public void SystemLocale_ResolvesToASupportedCatalog()
    {
        using var locale = new ActiveLocale("en");
        LocalizationCatalog selected = Localizer.Use(Localizer.SystemLocale);
        Assert.Contains(
            Localizer.Available,
            catalog => string.Equals(catalog.Locale, selected.Locale, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_RejectsMalformedCatalogs()
    {
        Assert.Throws<InvalidDataException>(() => LocalizationCatalog.Parse("not json", "bad"));
        Assert.Throws<InvalidDataException>(() => LocalizationCatalog.Parse("{}", "bad"));
        Assert.Throws<InvalidDataException>(() => LocalizationCatalog.Parse(
            """{"locale":"en","strings":{}}""",
            "empty"));
        Assert.Throws<InvalidDataException>(() => LocalizationCatalog.Parse(
            """{"locale":"en","strings":{"a":"x","a":"y"}}""",
            "duplicate"));
        Assert.Throws<InvalidDataException>(() => LocalizationCatalog.Parse(
            """{"locale":"en","strings":{"a":""}}""",
            "blank"));
        Assert.Throws<InvalidDataException>(() => LocalizationCatalog.Parse(
            """{"locale":"en","strings":{"a":1}}""",
            "number"));
    }

    [Fact]
    public void Parse_BuildsTheEnglishReverseMapAndKeepsFirstKey()
    {
        LocalizationCatalog catalog = LocalizationCatalog.Parse(
            """{"locale":"en","displayName":"English","strings":{"first":"Same","second":"Same","other":"Other"}}""",
            "test");

        Assert.Equal("Same", catalog.Get("first"));
        Assert.Equal("Other", catalog.Get("other"));
        Assert.Equal(3, catalog.Count);
    }

    [Fact]
    public void NormalizeLocale_HandlesSystemAndSeparators()
    {
        Assert.Equal(string.Empty, Localizer.NormalizeLocale(null));
        Assert.Equal(string.Empty, Localizer.NormalizeLocale(" "));
        Assert.Equal(string.Empty, Localizer.NormalizeLocale("SYSTEM"));
        Assert.Equal("de-at", Localizer.NormalizeLocale(" DE_AT "));
        Assert.Equal("en", Localizer.NormalizeLocale("en"));
    }

    private static LocalizationCatalog GetCatalog(string locale)
    {
        return Assert.Single(
            Localizer.Available,
            catalog => string.Equals(catalog.Locale, locale, StringComparison.Ordinal));
    }

    private sealed class ActiveLocale : IDisposable
    {
        private readonly string _original;

        public ActiveLocale(string locale)
        {
            _original = Localizer.Locale;
            if (!Localizer.TryUse(locale))
            {
                throw new InvalidOperationException($"Locale '{locale}' is not embedded.");
            }
        }

        public void Dispose()
        {
            _ = Localizer.TryUse(_original);
        }
    }
}
