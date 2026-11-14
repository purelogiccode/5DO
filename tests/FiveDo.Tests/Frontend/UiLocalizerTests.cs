using Avalonia.Controls;
using FiveDo.Frontend.Avalonia.Localization;
using FiveDo.Localization;

namespace FiveDo.Tests.Frontend;

/// <summary>
/// Verifies that the desktop localization pass can move between locales and back to
/// English without losing the original text. The localizer is process-wide, so these
/// tests share the non-parallel collection used by the localization tests.
/// </summary>
[Collection("Localizer")]
public sealed class UiLocalizerTests
{
    [Fact]
    public void SwitchingBackToEnglish_RestoresTheOriginalText()
    {
        var original = Localizer.Locale;
        try
        {
            Assert.True(Localizer.TryUse("de"));
            var block = new TextBlock { Text = "File" };
            UiLocalizer.Apply(block);
            Assert.Equal("Datei", block.Text);

            Assert.True(Localizer.TryUse(Localizer.DefaultLocale));
            UiLocalizer.Apply(block);
            Assert.Equal("File", block.Text);
        }
        finally
        {
            _ = Localizer.TryUse(original);
        }
    }

    [Fact]
    public void SwitchingBackToEnglish_RestoresToolTips()
    {
        var original = Localizer.Locale;
        try
        {
            Assert.True(Localizer.TryUse("de"));
            var block = new TextBlock { Text = "File" };
            ToolTip.SetTip(block, "Open a 3DO BIOS ROM");
            UiLocalizer.Apply(block);
            var german = ToolTip.GetTip(block);
            Assert.IsType<string>(german);
            Assert.NotEqual("Open a 3DO BIOS ROM", german);

            Assert.True(Localizer.TryUse(Localizer.DefaultLocale));
            UiLocalizer.Apply(block);
            Assert.Equal("Open a 3DO BIOS ROM", ToolTip.GetTip(block));
        }
        finally
        {
            _ = Localizer.TryUse(original);
        }
    }
}
