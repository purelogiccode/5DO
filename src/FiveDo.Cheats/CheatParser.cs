using System;
using System.Collections.Generic;
using System.Globalization;
using FiveDo.Input;

namespace FiveDo.Cheats;

/// <summary>
/// Parses 5DO cheat code text into executable statements.
/// </summary>
/// <remarks>
/// <para>
/// Numbers are hexadecimal by default. Prefix a number with <c>0d</c> for decimal or
/// <c>0x</c>/<c>$</c> for an explicit hexadecimal value. Statements are separated by
/// semicolons or newlines, and <c>#</c> or <c>//</c> starts a comment.
/// </para>
/// <para>
/// Supported statements are raw byte writes (<c>0xADDR = 12 34</c>), typed writes
/// (<c>write8</c>, <c>write16</c>, <c>write32</c>), and conditional writes
/// (<c>if 0xADDR == 0xVALUE then ...</c>). Conditions can compare guest memory,
/// test player-1 buttons, and combine with <c>and</c>, <c>or</c>, <c>not</c>, and
/// parentheses.
/// </para>
/// </remarks>
internal static class CheatParser
{
    public static CheatProgram Parse(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var statements = new List<CheatStatement>();
        foreach (var part in SplitStatements(code))
        {
            var tokens = Lex(part);
            var parser = new StatementParser(tokens, part);
            statements.Add(parser.ParseStatement());
            parser.ExpectEnd();
        }

        if (statements.Count == 0)
        {
            throw new FormatException("The cheat code has no statements.");
        }

        return new CheatProgram(statements.ToArray());
    }

    private static IEnumerable<string> SplitStatements(string code)
    {
        var normalized = code.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        foreach (var line in normalized.Split('\n'))
        {
            var withoutComment = StripComment(line);
            foreach (var part in withoutComment.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 0)
                {
                    yield return part;
                }
            }
        }
    }

    private static string StripComment(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            line = line[..hash];
        }

        var slash = line.IndexOf("//", StringComparison.Ordinal);
        if (slash >= 0)
        {
            line = line[..slash];
        }

        return line.Trim();
    }

    private static List<Token> Lex(string text)
    {
        var tokens = new List<Token>();
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (char.IsWhiteSpace(character))
            {
                index++;
                continue;
            }

            var position = index;
            if (character == '$')
            {
                index++;
                var start = index;
                while (index < text.Length && IsHexDigit(text[index]))
                {
                    index++;
                }

                if (index == start)
                {
                    throw CreateError("Expected hexadecimal digits after '$'.", position);
                }

                tokens.Add(NumberToken(text[start..index], position, 16));
                continue;
            }

            if (char.IsAsciiDigit(character))
            {
                var start = index;
                while (index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] == 'x' || text[index] == 'X'))
                {
                    index++;
                }

                tokens.Add(ParseNumberToken(text[start..index], position));
                continue;
            }

            if (char.IsAsciiLetter(character) || character == '_')
            {
                var start = index;
                while (index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] == '_'))
                {
                    index++;
                }

                tokens.Add(new Token(TokenKind.Identifier, text[start..index], 0, position));
                continue;
            }

            if (index + 1 < text.Length)
            {
                var pair = text.Substring(index, 2);
                if (pair is "==" or "!=" or "<=" or ">=")
                {
                    tokens.Add(new Token(TokenKind.Symbol, pair, 0, position));
                    index += 2;
                    continue;
                }
            }

            if (character is '(' or ')' or '=' or '<' or '>')
            {
                tokens.Add(new Token(TokenKind.Symbol, character.ToString(), 0, position));
                index++;
                continue;
            }

            throw CreateError($"Unexpected character '{character}'.", position);
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, 0, text.Length));
        return tokens;
    }

    private static Token ParseNumberToken(string text, int position)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("0X", StringComparison.Ordinal))
        {
            return NumberToken(text[2..], position, 16);
        }

        if (text.StartsWith("0d", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("0D", StringComparison.Ordinal))
        {
            return NumberToken(text[2..], position, 10);
        }

        return NumberToken(text, position, 16);
    }

    private static Token NumberToken(string text, int position, int numberBase)
    {
        if (text.Length == 0)
        {
            throw CreateError("Expected a number.", position);
        }

        if (!ulong.TryParse(text, numberBase == 16 ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw CreateError($"'{text}' is not a valid {(numberBase == 16 ? "hexadecimal" : "decimal")} number.", position);
        }

        return new Token(TokenKind.Number, text, value, position);
    }

    private static bool IsHexDigit(char character)
    {
        return char.IsAsciiDigit(character) || character is >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }

    private static bool IsHexText(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (!IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static FormatException CreateError(string message, int position)
    {
        return new FormatException($"{message} (column {position + 1}).");
    }

    private enum TokenKind
    {
        Number,
        Identifier,
        Symbol,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text, ulong Number, int Position);

    private sealed class StatementParser
    {
        private readonly List<Token> _tokens;
        private readonly string _source;
        private int _index;

        public StatementParser(List<Token> tokens, string source)
        {
            _tokens = tokens;
            _source = source;
        }

        private Token Current => _tokens[_index];

        public CheatStatement ParseStatement()
        {
            if (MatchIdentifier("if"))
            {
                CheatCondition condition = ParseCondition();
                ExpectIdentifier("then");
                return new CheatConditionalStatement(condition, ParseStatement());
            }

            if (MatchIdentifier("write"))
            {
                return ParseRawWrite();
            }

            if (MatchIdentifier("write8"))
            {
                return ParseTypedWrite(1);
            }

            if (MatchIdentifier("write16"))
            {
                return ParseTypedWrite(2);
            }

            if (MatchIdentifier("write32"))
            {
                return ParseTypedWrite(4);
            }

            if (IsValueToken())
            {
                return ParseShorthandWrite();
            }

            throw Error("Expected a write or an 'if ... then ...' statement.", Current.Position);
        }

        public void ExpectEnd()
        {
            if (Current.Kind != TokenKind.End)
            {
                throw Error($"Unexpected token '{Current.Text}'.", Current.Position);
            }
        }

        private CheatWriteStatement ParseRawWrite()
        {
            var address = ReadAddress();
            ExpectSymbol("=");
            var bytes = new List<byte>();
            while (IsValueToken())
            {
                AppendBigEndian(bytes, ReadNumber());
            }

            if (bytes.Count == 0)
            {
                throw Error("A raw write needs at least one byte value.", Current.Position);
            }

            return new CheatWriteStatement(address, bytes.ToArray());
        }

        private CheatWriteStatement ParseTypedWrite(int size)
        {
            var address = ReadAddress();
            ExpectSymbol("=");
            var value = ReadNumber();
            var maximum = size switch
            {
                1 => 0xFFu,
                2 => 0xFFFFu,
                _ => uint.MaxValue,
            };
            if (value > maximum)
            {
                throw Error($"The value does not fit in {size * 8} bits.", Current.Position);
            }

            return new CheatWriteStatement(address, Encode(value, size));
        }

        private CheatWriteStatement ParseShorthandWrite()
        {
            var address = ReadAddress();
            ExpectSymbol("=");
            var bytes = new List<byte>();
            while (IsValueToken())
            {
                AppendBigEndian(bytes, ReadNumber());
            }

            if (bytes.Count == 0)
            {
                throw Error("A raw write needs at least one byte value.", Current.Position);
            }

            return new CheatWriteStatement(address, bytes.ToArray());
        }

        private CheatCondition ParseCondition()
        {
            return ParseOr();
        }

        private CheatCondition ParseOr()
        {
            var left = ParseAnd();
            while (MatchIdentifier("or"))
            {
                left = new CheatLogicalCondition(left, ParseAnd(), isAnd: false);
            }

            return left;
        }

        private CheatCondition ParseAnd()
        {
            var left = ParseUnary();
            while (MatchIdentifier("and"))
            {
                left = new CheatLogicalCondition(left, ParseUnary(), isAnd: true);
            }

            return left;
        }

        private CheatCondition ParseUnary()
        {
            if (MatchIdentifier("not"))
            {
                return new CheatNotCondition(ParseUnary());
            }

            if (MatchSymbol("("))
            {
                var inner = ParseCondition();
                ExpectSymbol(")");
                return inner;
            }

            if (MatchIdentifier("button"))
            {
                return new CheatButtonCondition(ReadButton());
            }

            var size = 4;
            if (MatchIdentifier("byte"))
            {
                size = 1;
            }
            else if (MatchIdentifier("word"))
            {
                size = 2;
            }
            else if (MatchIdentifier("dword"))
            {
                size = 4;
            }

            return ParseMemoryCondition(size);
        }

        private CheatMemoryCondition ParseMemoryCondition(int size)
        {
            var address = ReadAddress();
            var comparison = Current.Kind == TokenKind.Symbol
                ? Current.Text switch
                {
                    "==" or "=" => CheatComparison.Equal,
                    "!=" => CheatComparison.NotEqual,
                    "<" => CheatComparison.Less,
                    "<=" => CheatComparison.LessOrEqual,
                    ">" => CheatComparison.Greater,
                    ">=" => CheatComparison.GreaterOrEqual,
                    _ => throw Error("Expected a comparison operator.", Current.Position),
                }
                : throw Error("Expected a comparison operator.", Current.Position);
            _index++;
            var value = ReadNumber();
            var maximum = size switch
            {
                1 => 0xFFu,
                2 => 0xFFFFu,
                _ => uint.MaxValue,
            };
            if (value > maximum)
            {
                throw Error($"The comparison value does not fit in {size * 8} bits.", Current.Position);
            }

            return new CheatMemoryCondition(address, size, comparison, (uint)value);
        }

        private ControllerButton ReadButton()
        {
            if (Current.Kind != TokenKind.Identifier)
            {
                throw Error("Expected a button name.", Current.Position);
            }

            var name = Current.Text;
            _index++;
            return name.ToUpperInvariant() switch
            {
                "A" => ControllerButton.A,
                "B" => ControllerButton.B,
                "C" => ControllerButton.C,
                "P" or "START" or "PAUSE" => ControllerButton.P,
                "X" => ControllerButton.X,
                "L" => ControllerButton.L,
                "R" => ControllerButton.R,
                "UP" => ControllerButton.Up,
                "DOWN" => ControllerButton.Down,
                "LEFT" => ControllerButton.Left,
                "RIGHT" => ControllerButton.Right,
                _ => throw Error($"Unknown button '{name}'.", Current.Position - 1),
            };
        }

        private uint ReadAddress()
        {
            var value = ReadNumber();
            if (value > uint.MaxValue)
            {
                throw Error("The address must fit in 32 bits.", Current.Position);
            }

            return (uint)value;
        }

        private ulong ReadNumber()
        {
            if (Current.Kind == TokenKind.Number)
            {
                var value = Current.Number;
                _index++;
                return value;
            }

            if (Current.Kind == TokenKind.Identifier && IsHexText(Current.Text))
            {
                if (!ulong.TryParse(
                        Current.Text,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var hexValue))
                {
                    throw Error($"'{Current.Text}' is not a valid hexadecimal number.", Current.Position);
                }

                _index++;
                return hexValue;
            }

            throw Error("Expected a number.", Current.Position);
        }

        private bool IsValueToken()
        {
            return Current.Kind == TokenKind.Number
                || (Current.Kind == TokenKind.Identifier && IsHexText(Current.Text));
        }

        private bool MatchIdentifier(string text)
        {
            if (Current.Kind != TokenKind.Identifier
                || !string.Equals(Current.Text, text, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _index++;
            return true;
        }

        private void ExpectIdentifier(string text)
        {
            if (!MatchIdentifier(text))
            {
                throw Error($"Expected '{text}'.", Current.Position);
            }
        }

        private bool MatchSymbol(string text)
        {
            if (Current.Kind != TokenKind.Symbol
                || !string.Equals(Current.Text, text, StringComparison.Ordinal))
            {
                return false;
            }

            _index++;
            return true;
        }

        private void ExpectSymbol(string text)
        {
            if (!MatchSymbol(text))
            {
                throw Error($"Expected '{text}'.", Current.Position);
            }
        }

        private FormatException Error(string message, int position)
        {
            return new FormatException(
                $"{message} (column {position + 1}) in '{_source}'.");
        }
    }

    private static void AppendBigEndian(List<byte> bytes, ulong value)
    {
        if (value <= 0xFF)
        {
            bytes.Add((byte)value);
            return;
        }

        if (value <= 0xFFFF)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value & 0xFF));
            return;
        }

        if (value <= 0xFFFFFFFF)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value & 0xFF));
            return;
        }

        throw new FormatException("A raw byte value must fit in 32 bits.");
    }

    private static byte[] Encode(ulong value, int size)
    {
        var bytes = new byte[size];
        for (var i = 0; i < size; i++)
        {
            bytes[i] = (byte)(value >> (i * 8));
        }

        return bytes;
    }
}
