using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Polhem.Markdown.Formulas;

namespace Polhem.Markdown.Avalonia.Formulas;

/// <summary>
/// Lays a formula out with the viewer's font: each part is measured as the text engine draws it, and placed by rules
/// after TeX's, simplified: scripts smaller and raised or lowered, fractions about the math axis, delimiters grown to
/// their content, limits below and above large operators in a formula shown on its own.
/// </summary>
/// <remarks>
/// Vertical placement uses the ink of each glyph, not the font's line height, so an accent sits on the letter it marks.
/// </remarks>
internal sealed class FormulaLayout
{
    private const double ScriptScale = 0.7;
    private const double SmallestScale = 0.5;

    private readonly FontFamily _family;
    private readonly double _fontSize;
    private readonly ISet<Type> _laidOut;

    public FormulaLayout(FontFamily family, double fontSize, ISet<Type> laidOut)
    {
        _family = family;
        _fontSize = fontSize;
        _laidOut = laidOut;
    }

    /// <summary>Lays out <paramref name="formula"/>, in display style when it is shown on its own.</summary>
    public FormulaBox Layout(FormulaItem formula, bool display) => Box(formula, level: 0, display);

    private double Size(int level) => _fontSize * Math.Max(SmallestScale, Math.Pow(ScriptScale, level));

    /// <summary>The height of the math axis above the baseline, where fraction bars and the middle of delimiters lie.</summary>
    private double Axis(int level) => Size(level) * 0.25;

    private double Rule(int level) => Math.Max(1, Size(level) * 0.06);

    private FormulaBox Box(FormulaItem item, int level, bool display)
    {
        var box = item switch
        {
            FormulaRow row => Row(row, level, display),
            FormulaSymbol symbol => Symbol(symbol, level, display),
            FormulaScripts scripts => Scripts(scripts, level, display),
            FormulaFraction fraction => Fraction(fraction, level, display),
            FormulaRoot root => Root(root, level, display),
            FormulaFenced fenced => Fenced(fenced, level, display),
            FormulaMatrix matrix => Matrix(matrix, level),
            FormulaAccent accent => Accent(accent, level, display),
            FormulaSpace space => new FormulaBox(space.Ems * Size(level), 0, 0, []),
            _ => null,
        };
        if (box is null)
        {
            return FormulaBox.Empty;
        }
        _laidOut.Add(item.GetType());
        return box;
    }

    private FormulaBox Row(FormulaRow row, int level, bool display)
    {
        var pieces = new List<FormulaPiece>();
        double x = 0, ascent = 0, descent = 0;
        foreach (var child in row.Items)
        {
            var box = Box(child, level, display);
            pieces.AddRange(box.PiecesAt(x, 0));
            x += box.Width;
            ascent = Math.Max(ascent, box.Ascent);
            descent = Math.Max(descent, box.Descent);
        }
        return new FormulaBox(x, ascent, descent, pieces);
    }

    private FormulaBox Symbol(FormulaSymbol symbol, int level, bool display)
    {
        var size = Size(level);
        size *= symbol.Kind switch
        {
            FormulaSymbolKind.LargeOperator or FormulaSymbolKind.Integral => display ? 1.6 : 1.15,
            _ => 1,
        };
        var italic = symbol.Kind == FormulaSymbolKind.Identifier;
        var text = Text(symbol.Text, size, italic);
        var (ascent, descent) = Ink(symbol.Text, size);
        var spacing = symbol.Kind == FormulaSymbolKind.Operator ? OperatorSpacing(symbol.Text, level) : (0, 0);
        if (symbol.Kind is FormulaSymbolKind.LargeOperator or FormulaSymbolKind.Integral)
        {
            // A large operator is centered on the math axis.
            var shift = (ascent - descent) / 2 - Axis(level);
            return new FormulaBox(text.WidthIncludingTrailingWhitespace + 0.1 * Size(level), ascent - shift, descent + shift,
                [new FormulaTextPiece(new Point(0.05 * Size(level), shift), symbol.Text, text, 1, 1)]);
        }
        if (symbol.Kind is FormulaSymbolKind.Name or FormulaSymbolKind.LimitName && symbol.Text.Length > 1)
        {
            // A function name such as `sin` is set off from what follows by a thin space.
            spacing = (0, Size(level) / 6);
        }
        var width = spacing.Item1 + text.WidthIncludingTrailingWhitespace + spacing.Item2;
        return new FormulaBox(width, ascent, descent, [new FormulaTextPiece(new Point(spacing.Item1, 0), symbol.Text, text, 1, 1)]);
    }

    /// <summary>
    /// The space around an operator: more around a relation or an arrow, less around a binary operator, after punctuation
    /// only, and none in scripts.
    /// </summary>
    private (double Before, double After) OperatorSpacing(string text, int level)
    {
        if (level > 0)
        {
            return (0, 0);
        }
        var em = Size(level);
        return text switch
        {
            "," or ";" or ":" => (0, em / 6),
            "+" or "−" or "×" or "÷" or "⋅" or "±" or "∓" or "∗" or "∘" or "∩"
                or "∪" or "∧" or "∨" or "⊕" or "⊗" or "∖" or "⊙" or "⊖" or "⋆"
                or "∙" => (em * 4 / 18, em * 4 / 18),
            "=" or "<" or ">" or "≤" or "≥" or "≠" or "≈" or "≡" or "∼" or "≃" or "≅"
                or "∝" or "∈" or "∉" or "∋" or "⊂" or "⊃" or "⊆" or "⊇" or "≪"
                or "≫" or "⊥" or "∥" or "∣" or "≺" or "≻" or "→" or "←" or "↔"
                or "⇒" or "⇐" or "⇔" or "⟺" or "⟹" or "⟸" or "↦" or "↑" or "↓"
                or "⟶" or "⟵" or "⟼" => (em * 5 / 18, em * 5 / 18),
            _ => (0, 0),
        };
    }

    private FormulaBox Scripts(FormulaScripts scripts, int level, bool display)
    {
        var baseBox = Box(scripts.Base, level, display);
        var sub = scripts.Subscript is { } subscript ? Box(subscript, level + 1, display: false) : null;
        var sup = scripts.Superscript is { } superscript ? Box(superscript, level + 1, display: false) : null;
        var size = Size(level);

        if (display && scripts.Base is FormulaSymbol { Kind: FormulaSymbolKind.LargeOperator or FormulaSymbolKind.LimitName })
        {
            // Limits go below and above, centered on the operator.
            var width = new[] { baseBox.Width, sub?.Width ?? 0, sup?.Width ?? 0 }.Max();
            var gap = size * 0.15;
            var pieces = new List<FormulaPiece>(baseBox.PiecesAt((width - baseBox.Width) / 2, 0));
            var ascent = baseBox.Ascent;
            var descent = baseBox.Descent;
            if (sup is not null)
            {
                pieces.AddRange(sup.PiecesAt((width - sup.Width) / 2, -(ascent + gap + sup.Descent)));
                ascent += gap + sup.Height;
            }
            if (sub is not null)
            {
                pieces.AddRange(sub.PiecesAt((width - sub.Width) / 2, descent + gap + sub.Ascent));
                descent += gap + sub.Height;
            }
            return new FormulaBox(width, ascent, descent, pieces);
        }

        // An italic letter leans into its superscript, so the scripts start a little further right.
        var italic = scripts.Base is FormulaSymbol { Kind: FormulaSymbolKind.Identifier };
        var x = baseBox.Width + size * (italic ? 0.1 : 0.05);
        var all = new List<FormulaPiece>(baseBox.Pieces);
        double boxAscent = baseBox.Ascent, boxDescent = baseBox.Descent, right = baseBox.Width;
        var supShift = sup is null ? 0 : Math.Max(size * 0.4, baseBox.Ascent - sup.Ascent * 0.4);
        var subShift = sub is null ? 0 : Math.Max(size * 0.2, baseBox.Descent + sub.Ascent * 0.4);
        if (sup is not null && sub is not null)
        {
            // Keep a gap between the two scripts.
            var gap = (supShift - sup.Descent) - (sub.Ascent - subShift);
            if (gap < size * 0.15)
            {
                subShift += size * 0.15 - gap;
            }
        }
        if (sup is not null)
        {
            all.AddRange(sup.PiecesAt(x, -supShift));
            boxAscent = Math.Max(boxAscent, supShift + sup.Ascent);
            boxDescent = Math.Max(boxDescent, sup.Descent - supShift);
            right = Math.Max(right, x + sup.Width);
        }
        if (sub is not null)
        {
            all.AddRange(sub.PiecesAt(x, subShift));
            boxAscent = Math.Max(boxAscent, sub.Ascent - subShift);
            boxDescent = Math.Max(boxDescent, subShift + sub.Descent);
            right = Math.Max(right, x + sub.Width);
        }
        return new FormulaBox(right + size * 0.05, boxAscent, boxDescent, all);
    }

    private FormulaBox Fraction(FormulaFraction fraction, int level, bool display)
    {
        // In a formula on its own the parts keep their size; in a line they are smaller, as TeX sets them.
        var partLevel = display ? level : level + 1;
        var numerator = Box(fraction.Numerator, partLevel, display: false);
        var denominator = Box(fraction.Denominator, partLevel, display: false);
        var size = Size(level);
        var rule = fraction.HasLine ? Rule(level) : 0;
        var gap = size * (fraction.HasLine ? 0.12 : 0.25);
        var pad = size * 0.1;
        var width = Math.Max(numerator.Width, denominator.Width) + 2 * pad;
        var axis = Axis(level);

        var numeratorBaseline = -(axis + rule / 2 + gap + numerator.Descent);
        var denominatorBaseline = -axis + rule / 2 + gap + denominator.Ascent;
        var pieces = new List<FormulaPiece>();
        pieces.AddRange(numerator.PiecesAt((width - numerator.Width) / 2, numeratorBaseline));
        pieces.AddRange(denominator.PiecesAt((width - denominator.Width) / 2, denominatorBaseline));
        if (fraction.HasLine)
        {
            pieces.Add(new FormulaRulePiece(new Rect(pad / 2, -axis - rule / 2, width - pad, rule)));
        }
        return new FormulaBox(width, -numeratorBaseline + numerator.Ascent, denominatorBaseline + denominator.Descent, pieces);
    }

    private FormulaBox Root(FormulaRoot root, int level, bool display)
    {
        var radicand = Box(root.Radicand, level, display);
        var size = Size(level);
        var rule = Rule(level);
        var gap = size * 0.12;
        var signWidth = size * 0.55;
        var top = Math.Max(radicand.Ascent, size * 0.5) + gap;
        var bottom = Math.Max(radicand.Descent, size * 0.1);

        var index = root.Index is { } written ? Box(written, level + 2, display: false) : null;
        var indent = index is null ? 0 : Math.Max(0, index.Width - signWidth * 0.45);

        var pieces = new List<FormulaPiece>();
        if (index is not null)
        {
            pieces.AddRange(index.PiecesAt(indent + signWidth * 0.45 - index.Width, -(top * 0.45) - index.Descent));
        }
        var left = indent;
        var height = top + bottom;
        pieces.Add(new FormulaStrokePiece(
        [
            new Point(left, -top * 0.35 + height * 0.1),
            new Point(left + signWidth * 0.2, -top * 0.45),
            new Point(left + signWidth * 0.5, bottom),
            new Point(left + signWidth, -top + rule / 2),
        ], rule));
        pieces.Add(new FormulaRulePiece(new Rect(left + signWidth, -top, radicand.Width + size * 0.1, rule)));
        pieces.AddRange(radicand.PiecesAt(left + signWidth + size * 0.05, 0));

        var ascent = Math.Max(top + rule, index is null ? 0 : top * 0.45 + index.Height);
        return new FormulaBox(left + signWidth + radicand.Width + size * 0.15, ascent, bottom, pieces);
    }

    private FormulaBox Fenced(FormulaFenced fenced, int level, bool display)
    {
        var content = Box(fenced.Content, level, display);
        var axis = Axis(level);

        // A delimiter covers the content symmetrically about the axis.
        var half = Math.Max(Math.Max(content.Ascent - axis, content.Descent + axis), Size(level) * 0.5);
        var pieces = new List<FormulaPiece>();
        var x = 0.0;
        var open = Delimiter(fenced.Open, level, half);
        if (open is not null)
        {
            pieces.AddRange(open.PiecesAt(x, 0));
            x += open.Width;
        }
        pieces.AddRange(content.PiecesAt(x, 0));
        x += content.Width;
        var close = Delimiter(fenced.Close, level, half);
        if (close is not null)
        {
            pieces.AddRange(close.PiecesAt(x, 0));
            x += close.Width;
        }
        return new FormulaBox(x, Math.Max(content.Ascent, axis + half), Math.Max(content.Descent, half - axis), pieces);
    }

    /// <summary>A delimiter grown vertically to reach <paramref name="half"/> above and below the axis.</summary>
    private FormulaBox? Delimiter(string delimiter, int level, double half)
    {
        if (delimiter.Length == 0)
        {
            return null;
        }
        var text = Text(delimiter, Size(level), italic: false);
        var (ascent, descent) = Ink(delimiter, Size(level));
        var ink = Math.Max(ascent + descent, 1);
        var scale = Math.Max(1, 2 * half / ink);

        // Scaled about its baseline, the glyph's ink is then moved to center on the axis.
        var center = (ascent - descent) / 2 * scale;
        var y = center - Axis(level);
        var pad = Size(level) * 0.05;
        return new FormulaBox(text.WidthIncludingTrailingWhitespace + 2 * pad, ascent * scale - y, descent * scale + y,
            [new FormulaTextPiece(new Point(pad, y), delimiter, text, 1, scale)]);
    }

    private FormulaBox Matrix(FormulaMatrix matrix, int level)
    {
        var size = Size(level);
        var cells = matrix.Rows.Select(row => row.Select(cell => Box(cell, level, display: false)).ToList()).ToList();
        var columns = cells.Select(row => row.Count).DefaultIfEmpty(0).Max();
        var widths = Enumerable.Range(0, columns)
            .Select(column => cells.Where(row => column < row.Count).Select(row => row[column].Width).DefaultIfEmpty(0).Max())
            .ToList();
        var rowGap = size * 0.35;
        var columnGap = size * (matrix.AlignLeft ? 1.0 : 0.8);

        var pieces = new List<FormulaPiece>();
        var y = 0.0;
        foreach (var row in cells)
        {
            var ascent = row.Select(cell => Math.Max(cell.Ascent, size * 0.7)).DefaultIfEmpty(size * 0.7).Max();
            var descent = row.Select(cell => Math.Max(cell.Descent, size * 0.25)).DefaultIfEmpty(size * 0.25).Max();
            var x = 0.0;
            for (var column = 0; column < row.Count; column++)
            {
                var cell = row[column];
                var offset = matrix.AlignLeft ? 0 : (widths[column] - cell.Width) / 2;
                pieces.AddRange(cell.PiecesAt(x + offset, y + ascent));
                x += widths[column] + columnGap;
            }
            y += ascent + descent + rowGap;
        }
        var height = Math.Max(0, y - rowGap);
        var width = widths.Sum() + columnGap * Math.Max(0, columns - 1);

        // The grid is centered on the axis.
        var shift = height / 2 + Axis(level);
        return new FormulaBox(width, shift, height - shift, [.. pieces.Select(piece => piece.MovedBy(0, -shift))]);
    }

    private FormulaBox Accent(FormulaAccent accent, int level, bool display)
    {
        var baseBox = Box(accent.Base, level, display);
        var size = Size(level);
        var gap = size * 0.08;
        var pieces = new List<FormulaPiece>(baseBox.Pieces);
        if (accent.Mark == "‾")
        {
            // An overline is a rule over the whole base.
            var rule = Rule(level);
            pieces.Add(new FormulaRulePiece(new Rect(0, -(baseBox.Ascent + gap + rule), baseBox.Width, rule)));
            return new FormulaBox(baseBox.Width, baseBox.Ascent + gap + rule, baseBox.Descent, pieces);
        }

        var mark = Text(accent.Mark, size, italic: false);
        var (top, bottom) = GlyphInk.Of(accent.Mark) ?? (0.75, 0.55);
        var markWidth = Math.Max(mark.WidthIncludingTrailingWhitespace, 1);
        var scaleX = accent.Stretches ? Math.Max(1, baseBox.Width / markWidth) : 1;
        var width = Math.Max(baseBox.Width, markWidth * scaleX);

        // The mark's ink bottom sits a little above the base's ink top, both centered.
        var baseline = -(baseBox.Ascent + gap - bottom * size);
        var placed = new List<FormulaPiece>(baseBox.PiecesAt((width - baseBox.Width) / 2, 0))
        {
            new FormulaTextPiece(new Point((width - markWidth * scaleX) / 2, baseline), accent.Mark, mark, scaleX, 1),
        };
        return new FormulaBox(width, Math.Max(baseBox.Ascent, top * size - baseline), baseBox.Descent, placed);
    }

    private FormattedText Text(string text, double size, bool italic) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(_family, italic ? FontStyle.Italic : FontStyle.Normal),
        size,
        null);

    /// <summary>How far the ink of <paramref name="content"/> reaches above and below the baseline (see <see cref="GlyphInk"/>).</summary>
    private static (double Ascent, double Descent) Ink(string content, double size) =>
        GlyphInk.Of(content) is { } ink ? (Math.Max(0, ink.Top * size), Math.Max(0, -ink.Bottom * size)) : (0, 0);
}
