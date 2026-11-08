"""Generate the compact HQx rule table from the 4DO reference sources.

Usage (from the repository root, with the 4DO reference sources present):

    python tools/generate_hqx_rules.py

The table is derived from Maxim Stepin's hq2x/hq3x/hq4x and Cameron Zemek's C
port (LGPL 2.1+); see THIRD-PARTY-NOTICES.md. The generated file is checked in
so builds do not need Python or the reference sources.
"""
import re

ROOT = 'References/4DO-master/FreeDOCore/Filters'
OUT = 'src/FiveDo.Video/HqxRules.cs'
PATTERN_COUNT = 256
ROW_COUNT = 32
ROW_OFFSETS = {2: 0, 3: 4, 4: 13}
FILES = {
    2: f'{ROOT}/hq2x.cpp',
    3: f'{ROOT}/hq3x.cpp',
    4: f'{ROOT}/hq4x.cpp',
}

TOKEN = re.compile(
    r'\s*(case|if|else|break|Diff|Interp\d+|p\d\d|w|o|switch|pattern|'
    r'[A-Za-z_]\w*|\d+|[{}\[\](),;:=])')
PIXELS = {'p00': 0, 'p01': 1, 'p10': 2, 'p11': 3}
CONDITIONS = {
    (2, 4): 0,
    (2, 6): 1,
    (4, 8): 2,
    (6, 8): 3,
}
INTERP_WEIGHTS = {
    1: ([3, 1], 2),
    2: ([2, 1, 1], 2),
    3: ([7, 1], 3),
    4: ([2, 7, 7], 4),
    5: ([1, 1], 1),
    6: ([5, 2, 1], 3),
    7: ([6, 1, 1], 3),
    8: ([5, 3], 3),
    9: ([2, 3, 3], 3),
    10: ([14, 1, 1], 4),
}


def parse_macros(source):
    macros = {}
    for match in re.finditer(r'#define\s+(PIXEL\d\d)(?:_(\w+))?\s+(.+)', source):
        macros[(match.group(1), match.group(2) or '')] = match.group(3).strip()
    return macros


def macro_expression(body):
    body = body.strip().rstrip(';').strip()
    if body.startswith('*'):
        # Direct assignment; the right side is w[5].
        return body.split('=', 1)[1].strip()
    match = re.match(r'(Interp\d+)\(dp(?:\+[^,]+)?,\s*(.*)\)$', body)
    if not match:
        raise ValueError('Unrecognized macro: ' + body)
    return f'{match.group(1)}({match.group(2)})'


def switch_body(source):
    start = source.index('switch (pattern)')
    brace = source.index('{', start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == '{':
            depth += 1
        elif source[index] == '}':
            depth -= 1
            if depth == 0:
                return source[brace + 1:index]
    raise ValueError('Unterminated switch')


def translate_case(body, macros, scale):
    lines = []
    for raw in body.splitlines():
        line = raw.strip()
        if not line or line.startswith('//'):
            continue
        if line == 'break;':
            lines.append('break;')
            continue
        if line.startswith('if (Diff('):
            lines.append(line)
            continue
        if line in ('{', '}', 'else', 'else {'):
            lines.append(line)
            continue
        match = re.match(r'PIXEL(\d\d)(?:_(\w+))?$', line)
        if not match:
            raise ValueError('Unrecognized statement: ' + line)
        pixel = match.group(1)
        suffix = match.group(2) or ''
        expression = macro_expression(macros[(f'PIXEL{pixel}', suffix)])
        index = int(pixel[0]) * scale + int(pixel[1])
        lines.append(f'o[{index}] = {expression};')
    return lines


def tokenize(source):
    tokens = []
    index = 0
    while index < len(source):
        match = TOKEN.match(source, index)
        if not match:
            if source[index:].strip() == '':
                break
            raise ValueError(f'Unrecognized token at {source[index:index + 40]!r}')
        tokens.append(match.group(1))
        index = match.end()
    return tokens


class Parser:
    def __init__(self, tokens):
        self.tokens = tokens
        self.index = 0

    def peek(self):
        return self.tokens[self.index] if self.index < len(self.tokens) else None

    def next(self):
        value = self.peek()
        self.index += 1
        return value

    def expect(self, value):
        actual = self.next()
        if actual != value:
            raise ValueError(f'Expected {value!r}, got {actual!r} at {self.index}')

    def parse_block(self):
        self.expect('{')
        statements = []
        while self.peek() != '}':
            statements.append(self.parse_statement())
        self.expect('}')
        return statements

    def parse_statement(self):
        token = self.peek()
        if token == 'case':
            self.next()
            number = int(self.next())
            self.expect(':')
            return ('case', number)
        if token == 'break':
            self.next()
            self.expect(';')
            return ('break',)
        if token == 'if':
            self.next()
            self.expect('(')
            self.expect('Diff')
            self.expect('(')
            self.expect('w')
            self.expect('[')
            first = int(self.next())
            self.expect(']')
            self.expect(',')
            self.expect('w')
            self.expect('[')
            second = int(self.next())
            self.expect(']')
            self.expect(')')
            self.expect(')')
            true_branch = self.parse_block()
            false_branch = []
            if self.peek() == 'else':
                self.next()
                false_branch = self.parse_block()
            return ('if', first, second, true_branch, false_branch)
        if token == 'o':
            self.next()
            self.expect('[')
            output = int(self.next())
            self.expect(']')
            self.expect('=')
            expression = self.parse_expression()
            self.expect(';')
            return ('assign', output, expression)
        if token in PIXELS:
            self.next()
            self.expect('=')
            expression = self.parse_expression()
            self.expect(';')
            return ('assign', PIXELS[token], expression)
        if token == '{':
            return ('block', self.parse_block())
        raise ValueError(f'Unexpected token {token!r} at {self.index}')

    def parse_expression(self):
        token = self.next()
        if token == 'w':
            self.expect('[')
            index = int(self.next())
            self.expect(']')
            return ('w', index)
        match = re.fullmatch(r'Interp(\d+)', token)
        if match:
            self.expect('(')
            arguments = [self.parse_expression()]
            while self.peek() == ',':
                self.next()
                arguments.append(self.parse_expression())
            self.expect(')')
            return ('interp', int(match.group(1)), arguments)
        raise ValueError(f'Unexpected expression {token!r}')


def parse_file(path, scale):
    source = open(path, encoding='utf-8-sig').read()
    macros = parse_macros(source)
    body = switch_body(source)
    statements = []
    for match in re.finditer(r'case\s+(\d+)\s*:', body):
        number = int(match.group(1))
        brace = body.find('{', match.end())
        depth = 0
        case_body = ''
        for index in range(brace, len(body)):
            if body[index] == '{':
                depth += 1
            elif body[index] == '}':
                depth -= 1
                if depth == 0:
                    case_body = body[brace + 1:index]
                    break
        statements.append(('case', number))
        parser = Parser(tokenize('\n'.join(translate_case(case_body, macros, scale))))
        while parser.peek() is not None:
            statements.append(parser.parse_statement())
    return statements


def group_cases(statements):
    groups = {}
    labels = []
    current = []

    def flush():
        if labels:
            for label in labels:
                groups[label] = list(current)

    for statement in statements:
        if statement[0] == 'case':
            if current:
                flush()
                current = []
                labels = []
            labels.append(statement[1])
        else:
            current.append(statement)
    flush()
    return groups


def encode_rule(taps, shift, condition):
    while len(taps) < 3:
        taps.append((0, 4))
    if len(taps) != 3:
        raise ValueError('A rule has more than three taps.')
    weights = 0
    for index, (weight, tap) in enumerate(taps):
        if weight < 0 or weight > 15:
            raise ValueError(f'Weight out of range: {weight}')
        if tap < 0 or tap > 8:
            raise ValueError(f'Tap out of range: {tap}')
        weights += weight << (index * 8)
        weights |= tap << ((index * 8) + 4)
    if sum(weight for weight, _ in taps) != 1 << shift:
        raise ValueError(f'Weights do not sum to 1 << {shift}: {taps}')
    if condition not in (0, 1, 2, 3, 7):
        raise ValueError(f'Condition out of range: {condition}')
    return weights | (shift << 24) | (condition << 27)


def rule_for(expression):
    if expression[0] == 'w':
        return encode_rule([(1, expression[1] - 1)], 0, 7)
    _, number, arguments = expression
    if number not in INTERP_WEIGHTS:
        raise ValueError(f'Unknown interpolation: {number}')
    weights, shift = INTERP_WEIGHTS[number]
    if len(arguments) != len(weights):
        raise ValueError(f'Interp{number} arity mismatch: {expression}')
    taps = [(weight, argument[1] - 1) for weight, argument in zip(weights, arguments)]
    return encode_rule(taps, shift, 7)


def build_scale(path, scale):
    groups = group_cases(parse_file(path, scale))
    if len(groups) != PATTERN_COUNT:
        raise ValueError(f'{path} has {len(groups)} cases, expected {PATTERN_COUNT}')

    table = [0] * (scale * scale * PATTERN_COUNT * 2)
    center = encode_rule([(1, 4)], 0, 7)

    for pattern in range(PATTERN_COUNT):
        body = groups.get(pattern)
        if body is None:
            raise ValueError(f'{path} is missing pattern {pattern}')

        base = [center] * (scale * scale)
        alternate = [center] * (scale * scale)
        conditions = [7] * (scale * scale)

        def apply(statements, condition=None, true_branch=None):
            for statement in statements:
                if statement[0] == 'assign':
                    output = statement[1]
                    if output >= scale * scale:
                        raise ValueError(f'Output {output} is out of range for {scale}x')
                    rule = rule_for(statement[2])
                    if condition is None:
                        base[output] = rule
                        alternate[output] = rule
                        conditions[output] = 7
                    elif true_branch:
                        alternate[output] = (rule & ~(7 << 27)) | (condition << 27)
                        conditions[output] = condition
                    else:
                        base[output] = (rule & ~(7 << 27)) | (condition << 27)
                        conditions[output] = condition
                elif statement[0] == 'if':
                    pair = tuple(sorted((statement[1], statement[2])))
                    if pair not in CONDITIONS:
                        raise ValueError(f'Unknown condition pair: {pair}')
                    if condition is not None:
                        raise ValueError('Nested HQx conditions are not supported.')
                    apply(statement[3], CONDITIONS[pair], True)
                    apply(statement[4], CONDITIONS[pair], False)
                elif statement[0] == 'block':
                    apply(statement[1], condition, true_branch)

        apply(body)
        for subpixel in range(scale * scale):
            condition = conditions[subpixel]
            if condition != 7:
                base[subpixel] = (base[subpixel] & ~(7 << 27)) | (condition << 27)
                alternate[subpixel] = (alternate[subpixel] & ~(7 << 27)) | (condition << 27)
            offset = ((subpixel * PATTERN_COUNT) + pattern) * 2
            table[offset] = base[subpixel]
            table[offset + 1] = alternate[subpixel]
    return table


def combine(scales):
    rows = ROW_COUNT * PATTERN_COUNT * 2
    combined = [0] * rows
    for scale, table in scales.items():
        offset = ROW_OFFSETS[scale]
        for subpixel in range(scale * scale):
            source = subpixel * PATTERN_COUNT * 2
            destination = (offset + subpixel) * PATTERN_COUNT * 2
            combined[destination:destination + (PATTERN_COUNT * 2)] = \
                table[source:source + (PATTERN_COUNT * 2)]
    return combined


def emit(rules):
    header = '''// <auto-generated />
// HQx rule table derived from Maxim Stepin's hq2x/hq3x/hq4x and Cameron
// Zemek's C port (LGPL 2.1+). See the header of HqxFilter.cs and
// THIRD-PARTY-NOTICES.md. Regenerate with tools/generate_hqx_rules.py.
using System;

namespace FiveDo.Video;

internal static class HqxRuleTable
{
    internal const int PatternCount = 256;
    internal const int RowCount = 32;

    private static readonly uint[] RulesData =
    [
'''
    footer = '''    ];

    internal static ReadOnlySpan<uint> Rules => RulesData;

    internal static int RowOffset(int scale)
    {
        return scale switch
        {
            2 => 0,
            3 => 4,
            4 => 13,
            _ => throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                "The HQx scale must be 2, 3, or 4."),
        };
    }
}
'''
    lines = [header]
    for index in range(0, len(rules), 8):
        chunk = rules[index:index + 8]
        lines.append('        ' + ', '.join(f'0x{value:08X}u' for value in chunk) + ',\n')
    lines.append(footer)
    return ''.join(lines)


def main():
    scales = {scale: build_scale(path, scale) for scale, path in FILES.items()}
    rules = combine(scales)
    source = emit(rules)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as file:
        file.write(source)
    print(f'wrote {OUT} ({len(rules)} uints)')


if __name__ == '__main__':
    main()
