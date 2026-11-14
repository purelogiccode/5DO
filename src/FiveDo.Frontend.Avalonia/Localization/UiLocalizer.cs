using System;
using System.Collections.Generic;
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
/// skipped so a view-model value is never frozen by a local set. The pass is idempotent
/// and only touches presentation properties.
/// </remarks>
internal static class UiLocalizer
{
    /// <summary>Translates the static text in a window's logical tree.</summary>
    /// <param name="root">Root logical element, usually a window.</param>
    public static void Apply(ILogical root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Localizer.IsTranslated)
        {
            return;
        }

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
            var translated = Localizer.TranslateText(tip);
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

        var translated = Localizer.TranslateText(current);
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

        var translated = Localizer.TranslateText(text);
        if (!string.Equals(translated, text, StringComparison.Ordinal))
        {
            setter(translated);
        }
    }
}
