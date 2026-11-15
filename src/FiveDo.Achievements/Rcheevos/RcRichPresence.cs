// Ported from rcheevos (MIT) - src/rcheevos/richpresence.c.
using System;
using System.Collections.Generic;
using System.Text;

namespace FiveDo.Achievements.Rcheevos;

internal sealed class RcRichPresenceLookupItem
{
    public uint First;
    public uint Last;
    public RcRichPresenceLookupItem? Left;
    public RcRichPresenceLookupItem? Right;
    public string Label = string.Empty;
}

internal sealed class RcRichPresenceLookup
{
    public RcRichPresenceLookupItem? Root;
    public RcRichPresenceLookup? Next;
    public string Name = string.Empty;
    public string DefaultLabel = string.Empty;
    public RcFormat Format;
}

internal sealed class RcRichPresenceDisplayPart
{
    public RcRichPresenceDisplayPart? Next;
    public string Text = string.Empty;
    public RcRichPresenceLookup? Lookup;
    public RcOperand Value = new();
    public RcFormat DisplayType;
}

internal sealed class RcRichPresenceDisplay
{
    public RcTrigger Trigger = new();
    public RcRichPresenceDisplay? Next;
    public RcRichPresenceDisplayPart? Display;
    public bool HasRequiredHits;
}

/// <summary>A parsed rich-presence script.</summary>
internal sealed class RcRichPresence
{
    public RcRichPresenceDisplay? FirstDisplay;

    public RcRichPresenceLookup? FirstLookup;

    public RcValue? Values;

    internal List<RcValue> ValueList { get; } = [];

    internal List<RcMemRefBase> MemRefs { get; private set; } = [];

    public static RcRichPresence Parse(string script)
    {
        var parse = new RcParseState();
        RcRichPresence richPresence = ParseInternal(script, parse);
        richPresence.MemRefs = parse.MemRefs;
        return richPresence;
    }

    public void Update(RcPeek? peek, object? userData)
    {
        RcMemRefHelpers.UpdateMemRefs(MemRefs, peek, userData);

        foreach (RcValue value in ValueList)
        {
            if (value.EvaluateTyped(out RcTypedValue result, peek, userData))
            {
                RcMemRefHelpers.UpdateValue(ref value.Value, result.U32);
                value.Value.Type = result.Type;
            }
        }

        for (RcRichPresenceDisplay? display = FirstDisplay; display is not null; display = display.Next)
        {
            if (display.HasRequiredHits)
            {
                _ = display.Trigger.Test(peek, userData);
            }
        }
    }

    public string GetDisplayString(RcPeek? peek, object? userData)
    {
        for (RcRichPresenceDisplay? display = FirstDisplay; display is not null; display = display.Next)
        {
            if (display.Next is null)
            {
                return EvaluateDisplay(display.Display);
            }

            if (!display.HasRequiredHits)
            {
                _ = display.Trigger.Test(peek, userData);
            }

            if (display.Trigger.State == RcTriggerState.Triggered)
            {
                return EvaluateDisplay(display.Display);
            }
        }

        return string.Empty;
    }

    public string Evaluate(RcPeek? peek, object? userData)
    {
        Update(peek, userData);
        return GetDisplayString(peek, userData);
    }

    public void Reset()
    {
        for (RcRichPresenceDisplay? display = FirstDisplay; display is not null; display = display.Next)
        {
            display.Trigger.ResetTrigger();
        }

        foreach (RcValue value in ValueList)
        {
            value.Reset();
        }
    }

    private static RcRichPresence ParseInternal(string script, RcParseState parse)
    {
        var self = new RcRichPresence();
        var parser = new RcLineParser(script);
        RcRichPresenceLookup? firstLookup = null;
        RcRichPresenceLookup? lastLookup = null;
        int displayStart = -1;
        int displayLine = 0;

        if (script.Length == 0)
        {
            throw new RcParseException(RcError.MissingDisplayString);
        }

        while (!parser.End)
        {
            RcLine line = parser.NextLine();
            if (line.Text.StartsWith("Lookup:", StringComparison.Ordinal))
            {
                var lookup = new RcRichPresenceLookup
                {
                    Name = line.Text[7..],
                    Format = RcFormat.Lookup,
                };

                if (lastLookup is null)
                {
                    firstLookup = lookup;
                }
                else
                {
                    lastLookup.Next = lookup;
                }

                lastLookup = lookup;
                ParseLookup(lookup, parser);
            }
            else if (line.Text.StartsWith("Format:", StringComparison.Ordinal))
            {
                string name = line.Text[7..];
                if (name.Length == 11 && string.Equals(name, "Unformatted", StringComparison.Ordinal))
                {
                    _ = parser.NextLine();
                    continue;
                }

                var lookup = new RcRichPresenceLookup
                {
                    Name = name,
                    Format = RcFormat.Value,
                };

                if (lastLookup is null)
                {
                    firstLookup = lookup;
                }
                else
                {
                    lastLookup.Next = lookup;
                }

                lastLookup = lookup;

                RcLine formatLine = parser.NextLine();
                if (formatLine.Text.StartsWith("FormatType=", StringComparison.Ordinal))
                {
                    string format = formatLine.Text[11..];
                    if (format.Length > 63)
                    {
                        format = format[..63];
                    }

                    lookup.Format = RcFormatter.Parse(format);
                }
            }
            else if (line.Text.StartsWith("Display:", StringComparison.Ordinal))
            {
                displayStart = parser.Position;
                displayLine = parser.LinesRead;

                // Consume conditional display lines and comments so the first pass does
                // not mistake display text for another section.
                while (!parser.End)
                {
                    RcLine displayLineText = parser.NextLine();
                    if (displayLineText.Text.StartsWith('?')
                        || displayLineText.Text.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    break;
                }
            }
        }

        self.FirstLookup = firstLookup;
        self.Values = null;
        self.ValueList.Clear();

        if (displayStart >= 0)
        {
            parser.Position = displayStart;
            parser.LinesRead = displayLine;
            int savedLinesRead = displayLine;

            RcRichPresenceDisplay? lastDisplay = null;
            bool hasDefault = false;
            RcLine line = parser.NextLine();

            while (true)
            {
                if (line.Text.StartsWith('?'))
                {
                    int question = line.Text.IndexOf('?', 1);
                    if (question > 0)
                    {
                        RcRichPresenceDisplay display = ParseDisplay(
                            line.Text[(question + 1)..],
                            parse,
                            firstLookup);

                        var triggerCursor = new RcCursor(line.Text[1..]);
                        display.Trigger = RcTrigger.Parse(triggerCursor, parse);
                        if (triggerCursor.Position != question - 1)
                        {
                            throw new RcParseException(RcError.InvalidOperator);
                        }

                        display.HasRequiredHits = parse.HasRequiredHits;

                        if (lastDisplay is null)
                        {
                            self.FirstDisplay = display;
                        }
                        else
                        {
                            lastDisplay.Next = display;
                        }

                        lastDisplay = display;
                    }
                }
                else if (!line.Text.StartsWith("//", StringComparison.Ordinal))
                {
                    hasDefault = true;
                    break;
                }

                if (parser.End)
                {
                    break;
                }

                line = parser.NextLine();
            }

            if (!hasDefault)
            {
                throw new RcParseException(RcError.MissingDisplayString);
            }

            RcRichPresenceDisplay defaultDisplay = ParseDisplay(line.Text, parse, firstLookup);
            if (lastDisplay is null)
            {
                self.FirstDisplay = defaultDisplay;
            }
            else
            {
                lastDisplay.Next = defaultDisplay;
            }

            parser.LinesRead = savedLinesRead;
        }

        if (self.FirstDisplay is null)
        {
            throw new RcParseException(RcError.MissingDisplayString);
        }

        self.Values = null;
        foreach (RcValue value in parse.Variables)
        {
            self.ValueList.Add(value);
        }

        if (self.ValueList.Count > 0)
        {
            self.Values = self.ValueList[0];
            for (int index = 0; index < self.ValueList.Count - 1; index++)
            {
                self.ValueList[index].Next = self.ValueList[index + 1];
            }

            self.ValueList[^1].Next = null;
        }

        foreach (RcValue value in self.ValueList)
        {
            value.UpdateMemRefsEnabled = false;
        }

        for (RcRichPresenceDisplay? display = self.FirstDisplay; display is not null; display = display.Next)
        {
            display.Trigger.UpdateMemRefsEnabled = false;
        }

        return self;
    }

    private static void ParseLookup(RcRichPresenceLookup lookup, RcLineParser parser)
    {
        while (!parser.End)
        {
            RcLine line = parser.NextLine();
            if (line.Text.Length < 2)
            {
                if (line.Text.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                return;
            }

            if (line.Text.StartsWith("*=", StringComparison.Ordinal))
            {
                lookup.DefaultLabel = line.Text[2..];
                continue;
            }

            int equals = line.Text.IndexOf('=');
            if (equals < 0)
            {
                throw new RcParseException(RcError.MissingValue);
            }

            string label = line.Text[(equals + 1)..];
            var cursor = new RcCursor(line.Text);
            while (true)
            {
                uint first;
                if (cursor.Current == '0' && cursor.CharAt(1) == 'x')
                {
                    cursor.Skip(2);
                    if (!RcText.TryParseUInt32(cursor, 16, out first, out int end))
                    {
                        throw new RcParseException(RcError.InvalidConstOperand);
                    }

                    cursor.Position = end;
                }
                else
                {
                    if (!RcText.TryParseUInt32(cursor, 10, out first, out int end))
                    {
                        throw new RcParseException(RcError.InvalidConstOperand);
                    }

                    cursor.Position = end;
                }

                uint last = first;
                if (cursor.Current == '-')
                {
                    cursor.Advance();
                    if (cursor.Current == '0' && cursor.CharAt(1) == 'x')
                    {
                        cursor.Skip(2);
                        if (!RcText.TryParseUInt32(cursor, 16, out last, out int end))
                        {
                            throw new RcParseException(RcError.InvalidConstOperand);
                        }

                        cursor.Position = end;
                    }
                    else
                    {
                        if (!RcText.TryParseUInt32(cursor, 10, out last, out int end))
                        {
                            throw new RcParseException(RcError.InvalidConstOperand);
                        }

                        cursor.Position = end;
                    }
                }

                while (cursor.Current == ' ')
                {
                    cursor.Advance();
                }

                if (cursor.Current == '=')
                {
                    InsertLookupItem(lookup, first, last, label);
                    break;
                }

                if (cursor.Current != ',')
                {
                    throw new RcParseException(RcError.InvalidConstOperand);
                }

                InsertLookupItem(lookup, first, last, label);
                cursor.Advance();
            }
        }
    }

    private static void InsertLookupItem(RcRichPresenceLookup lookup, uint first, uint last, string label)
    {
        RcRichPresenceLookupItem? item = lookup.Root;
        RcRichPresenceLookupItem? previous = null;
        bool right = false;

        while (item is not null)
        {
            if (first > item.Last)
            {
                if (first == item.Last + 1 && string.Equals(label, item.Label, StringComparison.Ordinal))
                {
                    item.Last = last;
                    return;
                }

                previous = item;
                right = true;
                item = item.Right;
            }
            else if (last < item.First)
            {
                if (last == item.First - 1 && string.Equals(label, item.Label, StringComparison.Ordinal))
                {
                    item.First = first;
                    return;
                }

                previous = item;
                right = false;
                item = item.Left;
            }
            else
            {
                throw new RcParseException(RcError.DuplicatedValue);
            }
        }

        var created = new RcRichPresenceLookupItem
        {
            First = first,
            Last = last,
            Label = label,
        };

        if (previous is null)
        {
            lookup.Root = created;
        }
        else if (right)
        {
            previous.Right = created;
        }
        else
        {
            previous.Left = created;
        }
    }

    private static RcRichPresenceDisplay ParseDisplay(string text, RcParseState parse, RcRichPresenceLookup? firstLookup)
    {
        if (text.Length < 1)
        {
            throw new RcParseException(RcError.MissingDisplayString);
        }

        var display = new RcRichPresenceDisplay();
        RcRichPresenceDisplayPart? lastPart = null;
        int position = 0;

        while (position < text.Length)
        {
            int at = position;
            while (at < text.Length && !(text[at] == '@' && (at == 0 || text[at - 1] != '\\')))
            {
                ++at;
            }

            if (at > position)
            {
                var part = new RcRichPresenceDisplayPart
                {
                    DisplayType = RcFormat.String,
                    Text = Unescape(text[position..at]),
                };

                if (lastPart is null)
                {
                    display.Display = part;
                }
                else
                {
                    lastPart.Next = part;
                }

                lastPart = part;
            }

            if (at >= text.Length)
            {
                break;
            }

            int macroStart = ++at;
            while (at < text.Length && text[at] != '(')
            {
                ++at;
            }

            if (at >= text.Length)
            {
                throw new RcParseException(RcError.MissingValue);
            }

            string macroName = text[macroStart..at];
            var macroPart = new RcRichPresenceDisplayPart
            {
                DisplayType = RcFormat.UnknownMacro,
                Text = macroName,
            };

            RcRichPresenceLookup? lookup = firstLookup;
            while (lookup is not null)
            {
                if (string.Equals(lookup.Name, macroName, StringComparison.Ordinal))
                {
                    macroPart.Lookup = lookup;
                    macroPart.DisplayType = lookup.Format;
                    macroPart.Text = lookup.Name;
                    break;
                }

                lookup = lookup.Next;
            }

            if (lookup is null)
            {
                RcFormat? builtin = macroName switch
                {
                    "Number" => RcFormat.Value,
                    "Score" => RcFormat.Score,
                    "Centiseconds" => RcFormat.Centisecs,
                    "Seconds" => RcFormat.Seconds,
                    "Minutes" => RcFormat.Minutes,
                    "SecondsAsMinutes" => RcFormat.SecondsAsMinutes,
                    "ASCIIChar" => RcFormat.AsciiChar,
                    "UnicodeChar" => RcFormat.UnicodeChar,
                    "Float1" => RcFormat.Float1,
                    "Float2" => RcFormat.Float2,
                    "Float3" => RcFormat.Float3,
                    "Float4" => RcFormat.Float4,
                    "Float5" => RcFormat.Float5,
                    "Float6" => RcFormat.Float6,
                    "Fixed1" => RcFormat.Fixed1,
                    "Fixed2" => RcFormat.Fixed2,
                    "Fixed3" => RcFormat.Fixed3,
                    "Unsigned" => RcFormat.UnsignedValue,
                    "Unformatted" => RcFormat.Unformatted,
                    _ => null,
                };

                if (builtin is { } format)
                {
                    macroPart.DisplayType = format;
                    macroPart.Text = macroName;
                }
            }

            int argumentStart = at + 1;
            int close = text.IndexOf(')', argumentStart);
            if (close < 0)
            {
                // Non-terminated macro: dump the macro and the rest of the line.
                macroPart.DisplayType = RcFormat.String;
                macroPart.Text = text[(macroStart - 1)..];
                macroPart.Lookup = null;
            }
            else if (macroPart.DisplayType != RcFormat.UnknownMacro)
            {
                string expression = text[argumentStart..close];
                RcValue value = AllocVariable(expression, parse);
                macroPart.Value = new RcOperand
                {
                    MemRef = value,
                    Type = RcOperandType.Address,
                    Size = RcMemSize.Bits32,
                    MemRefAccessType = RcOperandType.Address,
                };
            }
            else
            {
                macroPart.Text = text[macroStart..(close + 1)];
            }

            if (lastPart is null)
            {
                display.Display = macroPart;
            }
            else
            {
                lastPart.Next = macroPart;
            }

            lastPart = macroPart;
            position = close < 0 ? text.Length : close + 1;
        }

        return display;
    }

    private static RcValue AllocVariable(string expression, RcParseState parse)
    {
        foreach (RcValue existing in parse.Variables)
        {
            if (string.Equals(existing.Name, expression, StringComparison.Ordinal))
            {
                return existing;
            }
        }

        var cursor = new RcCursor(expression);
        RcValue value = RcValue.Parse(cursor, parse);
        value.Name = expression;
        parse.Variables.Add(value);
        return value;
    }

    private static string Unescape(string text)
    {
        if (text.IndexOf('\\') < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                continue;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    private static string EvaluateDisplay(RcRichPresenceDisplayPart? part)
    {
        if (part is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        RcRichPresenceDisplayPart? current = part;
        while (current is not null)
        {
            switch (current.DisplayType)
            {
                case RcFormat.String:
                    builder.Append(current.Text);
                    break;

                case RcFormat.Lookup:
                    {
                        RcOperand.Evaluate(out RcTypedValue value, current.Value, null);
                        RcTypedValue.Convert(ref value, RcValueType.Unsigned);

                        string label = current.Lookup?.DefaultLabel ?? string.Empty;
                        RcRichPresenceLookupItem? item = current.Lookup?.Root;
                        while (item is not null)
                        {
                            if (value.U32 > item.Last)
                            {
                                item = item.Right;
                            }
                            else if (value.U32 < item.First)
                            {
                                item = item.Left;
                            }
                            else
                            {
                                label = item.Label;
                                break;
                            }
                        }

                        builder.Append(label);
                        break;
                    }

                case RcFormat.AsciiChar:
                    {
                        while (current is not null && current.DisplayType == RcFormat.AsciiChar)
                        {
                            RcOperand.Evaluate(out RcTypedValue value, current.Value, null);
                            RcTypedValue.Convert(ref value, RcValueType.Unsigned);
                            if (value.U32 == 0)
                            {
                                while (current.Next is not null && current.Next.DisplayType == RcFormat.AsciiChar)
                                {
                                    current = current.Next;
                                }

                                break;
                            }

                            builder.Append(value.U32 is < 32 or >= 127 ? '?' : (char)value.U32);
                            if (current.Next is null || current.Next.DisplayType != RcFormat.AsciiChar)
                            {
                                break;
                            }

                            current = current.Next;
                        }

                        break;
                    }

                case RcFormat.UnicodeChar:
                    {
                        while (current is not null && current.DisplayType == RcFormat.UnicodeChar)
                        {
                            RcOperand.Evaluate(out RcTypedValue value, current.Value, null);
                            RcTypedValue.Convert(ref value, RcValueType.Unsigned);
                            if (value.U32 == 0)
                            {
                                while (current.Next is not null && current.Next.DisplayType == RcFormat.UnicodeChar)
                                {
                                    current = current.Next;
                                }

                                break;
                            }

                            uint code = value.U32;
                            if (code is < 32 or > 65535)
                            {
                                code = 0xFFFD;
                            }

                            if (code is >= 0xD800 and < 0xE000)
                            {
                                code = 0xFFFD;
                            }

                            builder.Append(char.ConvertFromUtf32((int)code));
                            if (current.Next is null || current.Next.DisplayType != RcFormat.UnicodeChar)
                            {
                                break;
                            }

                            current = current.Next;
                        }

                        break;
                    }

                case RcFormat.UnknownMacro:
                    builder.Append("[Unknown macro]").Append(current.Text);
                    break;

                default:
                    RcOperand.Evaluate(out RcTypedValue formatted, current.Value, null);
                    builder.Append(RcFormatter.FormatTyped(formatted, current.DisplayType));
                    break;
            }

            current = current?.Next;
        }

        return builder.ToString();
    }

    private readonly record struct RcLine(string Text);

    private sealed class RcLineParser(string text)
    {
        public string Text { get; } = text;

        public int Position { get; set; }

        public int LinesRead { get; set; }

        public bool End => Position >= Text.Length;

        public RcLine NextLine()
        {
            int lineStart = Position;
            int lineEnd = lineStart;
            while (lineEnd < Text.Length && Text[lineEnd] != '\n')
            {
                ++lineEnd;
            }

            int end = lineStart;
            while (end < lineEnd)
            {
                if (Text[end] == '/' && end + 1 < lineEnd && Text[end + 1] == '/'
                    && !(end > lineStart && Text[end - 1] == '\\'))
                {
                    break;
                }

                ++end;
            }

            if (end == lineEnd)
            {
                if (end > lineStart && Text[end - 1] == '\r')
                {
                    --end;
                }
            }
            else
            {
                while (end > lineStart && RcText.IsSpace(Text[end - 1]))
                {
                    --end;
                }
            }

            Position = lineEnd < Text.Length && Text[lineEnd] == '\n' ? lineEnd + 1 : lineEnd;
            ++LinesRead;

            return new RcLine(Text[lineStart..end]);
        }
    }
}

