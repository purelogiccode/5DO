using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FiveDo.Localization;

namespace FiveDo.Tests.Frontend;

/// <summary>
/// Guards the externalized-string baseline: every static, user-visible string in the
/// Avalonia views must exist in the English catalog so a locale can translate it.
/// </summary>
public sealed class LocalizationCoverageTests
{
    private static readonly HashSet<string> TextAttributes = new(StringComparer.Ordinal)
    {
        "Content",
        "Header",
        "PlaceholderText",
        "Text",
        "Title",
        "Watermark",
    };

    [Fact]
    public void EveryStaticViewString_ExistsInTheEnglishCatalog()
    {
        LocalizationCatalog english = Assert.Single(
            Localizer.Available,
            catalog => string.Equals(catalog.Locale, "en", StringComparison.Ordinal));
        var known = new HashSet<string>(english.Strings.Values, StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (FileInfo file in ViewFiles())
        {
            XDocument document = XDocument.Load(file.FullName);
            foreach (XElement element in document.Descendants())
            {
                foreach (XAttribute attribute in element.Attributes())
                {
                    if (!IsTranslatableAttribute(attribute.Name.LocalName))
                    {
                        continue;
                    }

                    var text = Normalize(attribute.Value);
                    if (text.Length == 0
                        || text.StartsWith('{')
                        || string.Equals(text, "5DO", StringComparison.Ordinal)
                        || string.Equals(text, "OK", StringComparison.Ordinal)
                        || !text.Any(char.IsLetter))
                    {
                        continue;
                    }

                    if (!known.Contains(text))
                    {
                        missing.Add($"{file.Name}: {text}");
                    }
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "Static view strings missing from Locales/en.json:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing.OrderBy(item => item, StringComparer.Ordinal)));
    }

    private static bool IsTranslatableAttribute(string name)
    {
        return TextAttributes.Contains(name)
            || string.Equals(name, "ToolTip.Tip", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IEnumerable<FileInfo> ViewFiles()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "5DO.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("The 5DO repository root could not be located.");
        }

        return new DirectoryInfo(Path.Combine(directory.FullName, "src", "FiveDo.Frontend.Avalonia"))
            .EnumerateFiles("*.axaml", SearchOption.AllDirectories)
            .Where(file => file.Directory?.Name is "Views" or "Styles" or "FiveDo.Frontend.Avalonia")
            .OrderBy(file => file.FullName, StringComparer.Ordinal);
    }
}
