namespace Polhem.Markdown.Formulas;

/// <summary>
/// Parses the subset of LaTeX math the viewers lay out: fractions, scripts, roots, Greek letters, operators, large
/// operators, delimiters, matrices, text and accents. It is all or nothing: an expression that uses anything else, or is
/// malformed, is not parsed, and the text shows its source.
/// </summary>
/// <remarks>
/// The parser reads each character once and nests no deeper than <see cref="MaxDepth"/>, so its time is linear in the
/// length of the source and its stack bounded, whatever the source holds.
/// </remarks>
internal sealed class LatexParser
{
    /// <summary>How deeply groups and arguments may nest; deeper sources show as written.</summary>
    internal const int MaxDepth = 32;

    /// <summary>The longest source parsed; a longer one shows as written.</summary>
    internal const int MaxLength = 10_000;

    private const char NoStop = '\0';

    private readonly string _source;
    private int _position;
    private int _depth;

    private LatexParser(string source)
    {
        _source = source;
    }

    /// <summary>Parses <paramref name="latex"/>, or returns <see langword="false"/> when it uses anything outside the subset.</summary>
    public static bool TryParse(string latex, out FormulaItem formula)
    {
        ArgumentNullException.ThrowIfNull(latex);
        formula = null!;
        if (latex.Length > MaxLength || string.IsNullOrWhiteSpace(latex))
        {
            return false;
        }
        var parser = new LatexParser(latex);
        try
        {
            formula = parser.Row(NoStop, inMatrix: false, inLeft: false);
            return parser._position == latex.Length;
        }
        catch (FormatException)
        {
            // NOTE: the parser signals an unsupported or malformed source by throwing, which unwinds the descent at once.
            return false;
        }
    }

    private static FormatException Unsupported() => new("The formula uses LaTeX outside the supported subset.");

    private bool AtEnd => _position >= _source.Length;

    private char Current => _source[_position];

    private FormulaRow Row(char stop, bool inMatrix, bool inLeft)
    {
        var items = new List<FormulaItem>();
        while (true)
        {
            SkipSpace();
            if (AtEnd)
            {
                if (stop != NoStop)
                {
                    throw Unsupported();
                }
                break;
            }
            if (Current == stop || (inMatrix && (Current == '&' || Peek("\\\\") || PeekCommand("end"))) || (inLeft && PeekCommand("right")))
            {
                break;
            }
            if (Current == '}' || Current == '&' || Peek("\\\\") || PeekCommand("right") || PeekCommand("end"))
            {
                throw Unsupported();
            }
            items.Add(Current is '^' or '_' or '\'' ? Scripts(new FormulaRow([])) : Scripts(Base()));
        }
        return new FormulaRow(items);
    }

    private FormulaItem Scripts(FormulaItem baseItem)
    {
        FormulaItem? subscript = null;
        FormulaItem? superscript = null;
        var primes = 0;
        while (true)
        {
            SkipSpace();
            if (AtEnd)
            {
                break;
            }
            if (Current == '^' && superscript is null)
            {
                _position++;
                superscript = Argument();
            }
            else if (Current == '_' && subscript is null)
            {
                _position++;
                subscript = Argument();
            }
            else if (Current == '\'' && superscript is null)
            {
                _position++;
                primes++;
            }
            else if (Current is '^' or '_')
            {
                // A second superscript or subscript on one base is an error in LaTeX too.
                throw Unsupported();
            }
            else
            {
                break;
            }
        }
        if (primes > 0)
        {
            var prime = new FormulaSymbol(new string('′', primes), FormulaSymbolKind.Operator);
            superscript = superscript is null ? prime : new FormulaRow([prime, superscript]);
        }
        return subscript is null && superscript is null ? baseItem : new FormulaScripts(baseItem, subscript, superscript);
    }

    /// <summary>The argument of a command or a script: a group, or a single symbol.</summary>
    private FormulaItem Argument()
    {
        SkipSpace();
        if (AtEnd)
        {
            throw Unsupported();
        }
        if (char.IsAsciiDigit(Current))
        {
            // `x^23` raises the 2 only, as in LaTeX.
            return new FormulaSymbol(_source[_position++].ToString(), FormulaSymbolKind.Number);
        }
        return Base();
    }

    private FormulaItem Base()
    {
        if (++_depth > MaxDepth)
        {
            throw Unsupported();
        }
        try
        {
            var c = Current;
            if (c == '{')
            {
                return Group();
            }
            if (c == '\\')
            {
                return Command();
            }
            if (char.IsAsciiDigit(c) || (c == '.' && _position + 1 < _source.Length && char.IsAsciiDigit(_source[_position + 1])))
            {
                var start = _position;
                while (!AtEnd && (char.IsAsciiDigit(Current) || (Current == '.' && _position + 1 < _source.Length && char.IsAsciiDigit(_source[_position + 1]))))
                {
                    _position++;
                }
                return new FormulaSymbol(_source[start.._position], FormulaSymbolKind.Number);
            }
            if (char.IsSurrogate(c))
            {
                // A character outside the Basic Multilingual Plane, such as 𝔽 or an emoji, is a pair of code units.
                if (!char.IsSurrogatePair(_source, _position))
                {
                    throw Unsupported();
                }
                var pair = _source.Substring(_position, 2);
                var letter = char.IsLetter(_source, _position);
                _position += 2;
                return new FormulaSymbol(pair, letter ? FormulaSymbolKind.Identifier : FormulaSymbolKind.Operator);
            }
            _position++;
            if (char.IsLetter(c))
            {
                return new FormulaSymbol(c.ToString(), FormulaSymbolKind.Identifier);
            }
            return c switch
            {
                '-' => new FormulaSymbol("−", FormulaSymbolKind.Operator),
                '*' => new FormulaSymbol("∗", FormulaSymbolKind.Operator),
                '~' => new FormulaSymbol(" ", FormulaSymbolKind.Text),
                '#' or '$' or '%' or '}' or '&' or '\\' or '^' or '_' => throw Unsupported(),
                _ when char.IsControl(c) => throw Unsupported(),
                _ => new FormulaSymbol(c.ToString(), FormulaSymbolKind.Operator),
            };
        }
        finally
        {
            _depth--;
        }
    }

    private FormulaRow Group()
    {
        Expect('{');
        var row = Row('}', inMatrix: false, inLeft: false);
        Expect('}');
        return row;
    }

    private FormulaItem Command()
    {
        _position++;
        if (AtEnd)
        {
            throw Unsupported();
        }
        if (!char.IsAsciiLetter(Current))
        {
            var symbol = Current;
            _position++;
            return symbol switch
            {
                ',' => new FormulaSpace(3 / 18.0),
                ':' or '>' => new FormulaSpace(4 / 18.0),
                ';' => new FormulaSpace(5 / 18.0),
                '!' => new FormulaSpace(-3 / 18.0),
                ' ' => new FormulaSpace(0.25),
                '{' or '}' => new FormulaSymbol(symbol.ToString(), FormulaSymbolKind.Operator),
                '|' => new FormulaSymbol("‖", FormulaSymbolKind.Operator),
                '%' or '$' or '&' or '#' or '_' => new FormulaSymbol(symbol.ToString(), FormulaSymbolKind.Text),
                _ => throw Unsupported(),
            };
        }

        var name = CommandName();
        if (LatexSymbols.Command(name) is { } known)
        {
            return known;
        }
        return name switch
        {
            "frac" or "dfrac" or "tfrac" => new FormulaFraction(Argument(), Argument(), HasLine: true),
            "binom" => new FormulaFenced("(", ")", new FormulaFraction(Argument(), Argument(), HasLine: false)),
            "sqrt" => Root(),
            "left" => Fenced(),
            "text" or "textrm" or "mbox" => new FormulaSymbol(RawGroup(), FormulaSymbolKind.Text),
            "mathrm" or "operatorname" => new FormulaSymbol(RawGroup(), FormulaSymbolKind.Name),
            "hat" => new FormulaAccent(Argument(), "ˆ", Stretches: false),
            "widehat" => new FormulaAccent(Argument(), "ˆ", Stretches: true),
            "bar" => new FormulaAccent(Argument(), "¯", Stretches: false),
            "overline" => new FormulaAccent(Argument(), "‾", Stretches: true),
            "vec" => new FormulaAccent(Argument(), "→", Stretches: false),
            "overrightarrow" => new FormulaAccent(Argument(), "→", Stretches: true),
            "tilde" => new FormulaAccent(Argument(), "˜", Stretches: false),
            "widetilde" => new FormulaAccent(Argument(), "˜", Stretches: true),
            "dot" => new FormulaAccent(Argument(), "˙", Stretches: false),
            "ddot" => new FormulaAccent(Argument(), "¨", Stretches: false),
            "quad" => new FormulaSpace(1),
            "qquad" => new FormulaSpace(2),
            "begin" => Environment(),
            _ => throw Unsupported(),
        };
    }

    private FormulaRoot Root()
    {
        SkipSpace();
        FormulaItem? index = null;
        if (!AtEnd && Current == '[')
        {
            _position++;
            index = Row(']', inMatrix: false, inLeft: false);
            Expect(']');
        }
        return new FormulaRoot(Argument(), index);
    }

    private FormulaFenced Fenced()
    {
        var open = Delimiter();
        var content = Row(NoStop, inMatrix: false, inLeft: true);
        if (!PeekCommand("right"))
        {
            throw Unsupported();
        }
        _position += "\\right".Length;
        return new FormulaFenced(open, Delimiter(), content);
    }

    /// <summary>The delimiter after <c>\left</c> or <c>\right</c>; a period is none.</summary>
    private string Delimiter()
    {
        SkipSpace();
        if (AtEnd)
        {
            throw Unsupported();
        }
        var c = Current;
        if (c == '\\')
        {
            _position++;
            var name = !AtEnd && !char.IsAsciiLetter(Current) ? _source[_position++].ToString() : CommandName();
            return LatexSymbols.Delimiter(name) ?? throw Unsupported();
        }
        _position++;
        return c switch
        {
            '.' => "",
            '(' or ')' or '[' or ']' or '|' or '/' => c.ToString(),
            _ => throw Unsupported(),
        };
    }

    private FormulaItem Environment()
    {
        var name = RawGroup();
        var (open, close, alignLeft) = name switch
        {
            "matrix" => ("", "", false),
            "pmatrix" => ("(", ")", false),
            "bmatrix" => ("[", "]", false),
            "Bmatrix" => ("{", "}", false),
            "vmatrix" => ("|", "|", false),
            "Vmatrix" => ("‖", "‖", false),
            "cases" => ("{", "", true),
            _ => throw Unsupported(),
        };

        if (++_depth > MaxDepth)
        {
            throw Unsupported();
        }
        var rows = new List<IReadOnlyList<FormulaItem>>();
        var cells = new List<FormulaItem>();
        while (true)
        {
            cells.Add(Row(NoStop, inMatrix: true, inLeft: false));
            SkipSpace();
            if (AtEnd)
            {
                throw Unsupported();
            }
            if (Current == '&')
            {
                _position++;
            }
            else if (Peek("\\\\"))
            {
                _position += 2;
                rows.Add(cells);
                cells = [];
            }
            else
            {
                _position += "\\end".Length;
                if (RawGroup() != name)
                {
                    throw Unsupported();
                }
                break;
            }
        }
        _depth--;

        // A trailing `\\` before `\end` adds no row.
        if (cells is not [FormulaRow { Items.Count: 0 }])
        {
            rows.Add(cells);
        }
        var matrix = new FormulaMatrix(rows, alignLeft);
        return open.Length == 0 && close.Length == 0 ? matrix : new FormulaFenced(open, close, matrix);
    }

    /// <summary>The text of a braced group as written, for <c>\text</c> and environment names; braces may nest.</summary>
    private string RawGroup()
    {
        SkipSpace();
        Expect('{');
        var start = _position;
        var depth = 1;
        for (; !AtEnd; _position++)
        {
            if (Current == '\\')
            {
                // An escaped brace does not open or close the group.
                _position++;
                continue;
            }
            if (Current == '{')
            {
                depth++;
            }
            else if (Current == '}' && --depth == 0)
            {
                var text = _source[start.._position];
                _position++;
                return Unescape(text);
            }
        }
        throw Unsupported();
    }

    private static string Unescape(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal) && !text.Contains('{', StringComparison.Ordinal))
        {
            return text;
        }
        var result = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var c = text[index];
            if (c == '\\' && index + 1 < text.Length && text[index + 1] is '{' or '}' or '%' or '$' or '&' or '#' or '_' or ' ')
            {
                result.Append(text[++index]);
            }
            else if (c == '\\')
            {
                throw Unsupported();
            }
            else if (c is not ('{' or '}'))
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    private string CommandName()
    {
        var start = _position;
        while (!AtEnd && char.IsAsciiLetter(Current))
        {
            _position++;
        }
        if (_position == start)
        {
            throw Unsupported();
        }
        return _source[start.._position];
    }

    private void Expect(char c)
    {
        SkipSpace();
        if (AtEnd || Current != c)
        {
            throw Unsupported();
        }
        _position++;
    }

    private void SkipSpace()
    {
        while (!AtEnd && char.IsWhiteSpace(Current))
        {
            _position++;
        }
    }

    private bool Peek(string text) => string.CompareOrdinal(_source, _position, text, 0, text.Length) == 0;

    /// <summary>Whether the command <paramref name="name"/>, and not a longer one, comes next.</summary>
    private bool PeekCommand(string name)
    {
        var after = _position + 1 + name.Length;
        return Peek("\\" + name) && (after >= _source.Length || !char.IsAsciiLetter(_source[after]));
    }
}
