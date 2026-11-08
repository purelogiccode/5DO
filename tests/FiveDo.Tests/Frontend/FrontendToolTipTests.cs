using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FiveDo.Tests.Frontend;

/// <summary>
/// Guards the desktop requirement that every interactive element in every window
/// carries a tooltip. The XAML files are parsed as XML so the check stays independent
/// of the Avalonia runtime and runs in headless CI.
/// </summary>
public sealed class FrontendToolTipTests
{
    private static readonly HashSet<string> InteractiveElements = new(StringComparer.Ordinal)
    {
        "AutoCompleteBox",
        "Button",
        "ButtonSpinner",
        "Calendar",
        "CheckBox",
        "ColorPicker",
        "ComboBox",
        "ComboBoxItem",
        "DatePicker",
        "ListBox",
        "ListBoxItem",
        "MenuItem",
        "NumericUpDown",
        "RadioButton",
        "Slider",
        "TabControl",
        "TabItem",
        "TextBox",
        "ToggleButton",
        "TreeView",
        "TreeViewItem",
    };

    public static TheoryData<string> ViewFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in ViewsDirectory().EnumerateFiles("*.axaml", SearchOption.TopDirectoryOnly)
                     .OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            data.Add(path.Name);
        }

        return data;
    }

    [Fact]
    public void ViewsDirectory_ContainsTheExpectedWindows()
    {
        Assert.NotEmpty(ViewFiles());
    }

    [Theory]
    [MemberData(nameof(ViewFiles))]
    public void EveryInteractiveElement_HasAToolTip(string fileName)
    {
        XDocument document = XDocument.Load(Path.Combine(ViewsDirectory().FullName, fileName));
        var missing = new List<string>();
        foreach (XElement element in document.Descendants())
        {
            if (!InteractiveElements.Contains(element.Name.LocalName))
            {
                continue;
            }

            if (!HasToolTipOnSelfOrAncestor(element))
            {
                var name = element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
                missing.Add(name is null
                    ? element.Name.LocalName
                    : $"{element.Name.LocalName} x:Name={name}");
            }
        }

        Assert.True(
            missing.Count == 0,
            $"{fileName} has interactive elements without ToolTip.Tip:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Theory]
    [MemberData(nameof(ViewFiles))]
    public void EveryWindow_DeclaresADialogOwnerOrCenterScreen(string fileName)
    {
        XDocument document = XDocument.Load(Path.Combine(ViewsDirectory().FullName, fileName));
        XElement root = document.Root ?? throw new InvalidOperationException($"{fileName} has no root element.");
        if (!string.Equals(root.Name.LocalName, "Window", StringComparison.Ordinal))
        {
            return;
        }

        var startupLocation = root.Attribute("WindowStartupLocation")?.Value;
        Assert.True(
            !string.IsNullOrWhiteSpace(startupLocation),
            $"{fileName} must set WindowStartupLocation so dialogs open predictably.");
    }

    private static bool HasToolTipOnSelfOrAncestor(XElement element)
    {
        for (XElement? current = element; current is not null; current = current.Parent)
        {
            if (current.Attributes().Any(attribute => string.Equals(
                    attribute.Name.LocalName,
                    "ToolTip.Tip",
                    StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static DirectoryInfo ViewsDirectory()
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

        return new DirectoryInfo(Path.Combine(directory.FullName, "src", "FiveDo.Frontend.Avalonia", "Views"));
    }
}
