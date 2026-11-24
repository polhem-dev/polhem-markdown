namespace Polhem.Markdown.Formulas;

/// <summary>How a <see cref="FormulaSymbol"/> is set.</summary>
internal enum FormulaSymbolKind
{
    /// <summary>A variable: italic, such as <c>x</c> or <c>α</c>.</summary>
    Identifier,

    /// <summary>An upright name or constant: <c>sin</c>, <c>Γ</c>, <c>∞</c>.</summary>
    Name,

    /// <summary>A name whose limits go below it in a block, such as <c>lim</c> or <c>max</c>.</summary>
    LimitName,

    /// <summary>A number.</summary>
    Number,

    /// <summary>An operator, relation, arrow or punctuation, spaced by its kind.</summary>
    Operator,

    /// <summary>A large operator whose limits go below and above it in a block, such as <c>∑</c>.</summary>
    LargeOperator,

    /// <summary>An integral sign: large, with its limits as scripts.</summary>
    Integral,

    /// <summary>Ordinary text, upright, with its spaces: <c>\text{…}</c>.</summary>
    Text,
}
