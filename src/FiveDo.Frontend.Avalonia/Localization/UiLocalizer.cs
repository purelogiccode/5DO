using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using FiveDo.Localization;

namespace FiveDo.Frontend.Avalonia.Localization;

/// <summary>
/// Applies the selected locale to an already-built Avalonia window.
/// </summary>
/// <remarks>
/// The desktop shell was written with English literals. Rather than replacing every
/// literal with a binding, the baseline locale catalog also records the English text of
/// each resource, and this pass translates the static labels, menus, headers, titles,
/// watermarks, and tooltips in the logical tree. Properties that are data-bound are
/// skipped so a view-model value is never frozen by a local set. The original text is
/// remembered per control and property, so switching back to English (or to another
/// locale) restores or retranslates the same string instead of leaving the previous
/// language in place. The pass is idempotent and only touches presentation properties.
/// </remarks>
internal static class UiLocalizer
{
    private static readonly ConditionalWeakTable<AvaloniaObject, Dictionary<AvaloniaProperty, LocalizedValue>>
        Originals = new();

    /// <summary>Translates the static text in a window's logical tree.</summary>
    /// <param name="root">Root logical element, usually a window.</param>
    public static void Apply(ILogical root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var pending = new Stack<ILogical>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            ILogical node = pending.Pop();
            if (node is Control control)
            {
                LocalizeControl(control);
            }

            foreach (ILogical child in node.GetLogicalChildren())
            {
                pending.Push(child);
            }
        }
    }

    private static void LocalizeControl(Control control)
    {
        switch (control)
        {
            case Window window:
                SetText(window, Window.TitleProperty, window.Title, value => window.Title = value);
                break;
            case TextBlock textBlock:
                SetText(textBlock, TextBlock.TextProperty, textBlock.Text, value => textBlock.Text = value);
                break;
            case TextBox textBox:
                SetText(
                    textBox,
                    TextBox.PlaceholderTextProperty,
                    textBox.PlaceholderText,
                    value => textBox.PlaceholderText = value);
                break;
        }

        if (control is ContentControl contentControl)
        {
            SetObjectText(
                contentControl,
                ContentControl.ContentProperty,
                contentControl.Content,
                value => contentControl.Content = value);
        }

        if (control is HeaderedContentControl headeredContent)
        {
            SetObjectText(
                headeredContent,
                HeaderedContentControl.HeaderProperty,
                headeredContent.Header,
                value => headeredContent.Header = value);
        }

        if (control is HeaderedItemsControl headeredItems)
        {
            SetObjectText(
                headeredItems,
                HeaderedItemsControl.HeaderProperty,
                headeredItems.Header,
                value => headeredItems.Header = value);
        }

        if (control is MenuItem menuItem)
        {
            SetObjectText(
                menuItem,
                HeaderedItemsControl.HeaderProperty,
                menuItem.Header,
                value => menuItem.Header = value);
        }

        if (ToolTip.GetTip(control) is string tip)
        {
            var translated = Translate(control, ToolTip.TipProperty, tip);
            if (!string.Equals(translated, tip, StringComparison.Ordinal))
            {
                ToolTip.SetTip(control, translated);
            }
        }
    }

    private static void SetText(
        AvaloniaObject target,
        AvaloniaProperty<string?> property,
        string? current,
        Action<string> setter)
    {
        if (string.IsNullOrEmpty(current)
            || BindingOperations.GetBindingExpressionBase(target, property) is not null)
        {
            return;
        }

        var translated = Translate(target, property, current);
        if (!string.Equals(translated, current, StringComparison.Ordinal))
        {
            setter(translated);
        }
    }

    private static void SetObjectText(
        AvaloniaObject target,
        AvaloniaProperty property,
        object? current,
        Action<object> setter)
    {
        if (current is not string text
            || text.Length == 0
            || BindingOperations.GetBindingExpressionBase(target, property) is not null)
        {
            return;
        }

        var translated = Translate(target, property, text);
        if (!string.Equals(translated, text, StringComparison.Ordinal))
        {
            setter(translated);
        }
    }

    /// <summary>
    /// Translates a presentation string through the remembered English original so the
    /// pass can move between locales and back to English without losing the source text.
    /// </summary>
    /// <param name="target">Control that owns the property.</param>
    /// <param name="property">Presentation property being translated.</param>
    /// <param name="current">Current value read from the control.</param>
    /// <returns>The value that should be present for the active locale.</returns>
    private static string Translate(AvaloniaObject target, AvaloniaProperty property, string current)
    {
        if (!Originals.TryGetValue(target, out Dictionary<AvaloniaProperty, LocalizedValue>? values))
        {
            values = [];
            Originals.Add(target, values);
        }

        if (!values.TryGetValue(property, out LocalizedValue? value))
        {
            value = new LocalizedValue(current);
            values.Add(property, value);
        }
        else if (!string.Equals(current, value.Original, StringComparison.Ordinal)
                 && !string.Equals(current, value.Applied, StringComparison.Ordinal))
        {
            // Another code path replaced the text after the previous localization pass;
            // treat the new value as the source text instead of overwriting it forever.
            value.Original = current;
        }

        value.Applied = Localizer.TranslateText(value.Original);
        return value.Applied;
    }

    private sealed class LocalizedValue
    {
        public LocalizedValue(string original)
        {
            Original = original;
            Applied = original;
        }

        public string Original { get; set; }

        public string Applied { get; set; }
    }
}
