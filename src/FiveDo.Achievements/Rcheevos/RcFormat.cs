// Ported from rcheevos (MIT) - src/rcheevos/format.c.
using System;
using System.Globalization;
using System.Text;

namespace FiveDo.Achievements.Rcheevos;

/// <summary>rcheevos value formatting.</summary>
internal static class RcFormatter
{
    public static RcFormat Parse(string format)
    {
        if (format.StartsWith("FRAMES", StringComparison.Ordinal))
        {
            return RcFormat.Frames;
        }

        if (format.StartsWith("FLOAT", StringComparison.Ordinal) && format.Length == 6
            && format[5] is >= '1' and <= '6')
        {
            return (RcFormat)((int)RcFormat.Float1 + (format[5] - '1'));
        }

        if (format.StartsWith("FIXED", StringComparison.Ordinal) && format.Length == 6
            && format[5] is >= '1' and <= '3')
        {
            return (RcFormat)((int)RcFormat.Fixed1 + (format[5] - '1'));
        }

        return format switch
        {
            "TIME" => RcFormat.Frames,
            "TIMESECS" => RcFormat.Seconds,
            "THOUSANDS" => RcFormat.Thousands,
            "TENS" => RcFormat.Tens,
            "SECS" => RcFormat.Seconds,
            "SCORE" => RcFormat.Score,
            "SECS_AS_MINS" => RcFormat.SecondsAsMinutes,
            "MILLISECS" => RcFormat.Centisecs,
            "MINUTES" => RcFormat.Minutes,
            "POINTS" => RcFormat.Score,
            "VALUE" => RcFormat.Value,
            "UNSIGNED" => RcFormat.UnsignedValue,
            "OTHER" => RcFormat.Score,
            "HUNDREDS" => RcFormat.Hundreds,
            _ => RcFormat.Value,
        };
    }

    public static string FormatValue(int value, RcFormat format)
    {
        RcTypedValue typed = RcTypedValue.FromSigned(value);
        return FormatTyped(typed, format);
    }

    public static string FormatTyped(RcTypedValue value, RcFormat format)
    {
        RcTypedValue converted = value;

        switch (format)
        {
            default:
            case RcFormat.Value:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return InsertCommas(converted.I32.ToString(CultureInfo.InvariantCulture));

            case RcFormat.Frames:
                RcTypedValue.Convert(ref converted, RcValueType.Unsigned);
                return FormatCentiseconds(converted.U32 * 10 / 6);

            case RcFormat.Centisecs:
                RcTypedValue.Convert(ref converted, RcValueType.Unsigned);
                return FormatCentiseconds(converted.U32);

            case RcFormat.Seconds:
                RcTypedValue.Convert(ref converted, RcValueType.Unsigned);
                return FormatSeconds(converted.U32);

            case RcFormat.SecondsAsMinutes:
                RcTypedValue.Convert(ref converted, RcValueType.Unsigned);
                return FormatMinutes(converted.U32 / 60);

            case RcFormat.Minutes:
                RcTypedValue.Convert(ref converted, RcValueType.Unsigned);
                return FormatMinutes(converted.U32);

            case RcFormat.Score:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return InsertCommas(converted.I32.ToString("D6", CultureInfo.InvariantCulture));

            case RcFormat.Float1:
            case RcFormat.Float2:
            case RcFormat.Float3:
            case RcFormat.Float4:
            case RcFormat.Float5:
            case RcFormat.Float6:
                RcTypedValue.Convert(ref converted, RcValueType.Float);
                return converted.F32.ToString("F" + ((int)format - (int)RcFormat.Float1 + 1), CultureInfo.InvariantCulture);

            case RcFormat.Fixed1:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return FormatFixed(converted.I32, 10, "D1");

            case RcFormat.Fixed2:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return FormatFixed(converted.I32, 100, "D2");

            case RcFormat.Fixed3:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return FormatFixed(converted.I32, 1000, "D3");

            case RcFormat.Tens:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return converted.I32 == 0 ? "0" : converted.I32.ToString(CultureInfo.InvariantCulture) + "0";

            case RcFormat.Hundreds:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return converted.I32 == 0 ? "0" : converted.I32.ToString(CultureInfo.InvariantCulture) + "00";

            case RcFormat.Thousands:
                RcTypedValue.Convert(ref converted, RcValueType.Signed);
                return converted.I32 == 0 ? "0" : converted.I32.ToString(CultureInfo.InvariantCulture) + "000";

            case RcFormat.UnsignedValue:
            case RcFormat.Unformatted:
                RcTypedValue.Convert(ref converted, RcValueType.Unsigned);
                return converted.U32.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string FormatFixed(int value, int factor, string digitsFormat)
    {
        int whole = value / factor;
        int fraction = Math.Abs(value % factor);
        return whole.ToString(CultureInfo.InvariantCulture)
            + "."
            + fraction.ToString(digitsFormat, CultureInfo.InvariantCulture);
    }

    private static string FormatSeconds(uint seconds)
    {
        uint minutes = seconds / 60;
        seconds -= minutes * 60;
        if (minutes < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{minutes}:{seconds:D2}");
        }

        uint hours = minutes / 60;
        minutes -= hours * 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours}h{minutes:D2}:{seconds:D2}");
    }

    private static string FormatCentiseconds(uint centiseconds)
    {
        uint seconds = centiseconds / 100;
        centiseconds -= seconds * 100;
        return FormatSeconds(seconds) + "." + centiseconds.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static string FormatMinutes(uint minutes)
    {
        uint hours = minutes / 60;
        minutes -= hours * 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours}h{minutes:D2}");
    }

    private static string InsertCommas(string text)
    {
        int start = text.Length > 0 && text[0] == '-' ? 1 : 0;
        int end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            ++end;
        }

        int digits = end - start;
        if (digits <= 3)
        {
            return text;
        }

        int commas = (digits - 1) / 3;
        var builder = new StringBuilder(text.Length + commas);
        builder.Append(text, 0, start + ((digits - 1) % 3) + 1);

        int index = start + ((digits - 1) % 3) + 1;
        while (index < end)
        {
            builder.Append(',');
            builder.Append(text, index, 3);
            index += 3;
        }

        builder.Append(text, end, text.Length - end);
        return builder.ToString();
    }
}
