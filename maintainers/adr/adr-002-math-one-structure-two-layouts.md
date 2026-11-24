# ADR-002: Math is parsed once and laid out by each viewer

## Status

**Accepted (2026-11-24)**

Part of rendering math ([#14](https://github.com/polhem-dev/polhem-markdown/issues/14)), which aligns the viewers with
how GitHub renders `.md` files ([#9](https://github.com/polhem-dev/polhem-markdown/issues/9)). The choices below were
made in [the issue](https://github.com/polhem-dev/polhem-markdown/issues/14#issuecomment-7120026296) and during the
pull request.

## Context

GitHub renders `$…$`, `$$…$$` and ` ```math ` blocks with MathJax. Neither viewer may run a script: the Blazor viewer
writes no script into the page, and the Avalonia viewer has no browser. Mermaid diagrams faced the same constraint and
are laid out in `Polhem.Markdown` and drawn from shapes ([#8](https://github.com/polhem-dev/polhem-markdown/issues/8)),
but a diagram's text only has to fit in a box, while math is typeset glyph by glyph: laying it out without the font's
real metrics would show.

Current browsers lay out MathML natively (Chrome since 109, Firefox, Safari). Blazor's render tree, however, creates every
element it does not know as HTML; it treats SVG specially but not MathML, so a `<math>` built there stays inert text.
Checked in the browser: the same markup parsed as markup gets the MathML namespace and is laid out.

On the Avalonia side, the text engine reports the line's extent for text, not the ink of its glyphs, and on the
platforms checked it gave neither glyph outlines (`FormattedText.BuildGeometry` returned nothing in the headless
renderer) nor glyph bounds (`GlyphTypeface.TryGetGlyphMetrics` returned advances only).

## Decision

### 1. One structure, parsed in the core

`Polhem.Markdown` recognizes the syntax with Markdig's own math extension (no new dependency) and parses a subset of
LaTeX into formula parts (`Formulas/FormulaItem.cs`): rows, symbols by kind, scripts, fractions, roots, delimiters,
matrices, accents and spaces. The parse is all or nothing: anything outside the subset, or malformed, leaves the source
shown as code in both viewers. The parser is linear and bounded in length and depth.

### 2. Blazor writes MathML as markup

The Blazor viewer writes each formula as a MathML string (`MathMlWriter`) and adds it as markup, so the browser parses it
into the MathML namespace. This is the one exception to building the page through the render tree. Element and
attribute names and attribute values are constants of the writer or numbers, and every text, the LaTeX source in the
annotation included, is HTML-encoded. `Render_Math_WritesOnlyMathMl` renders formulas with hostile text and fails when
any element or attribute outside the MathML set is written.

Building the MathML from script instead (a module calling `createElementNS`) was rejected: it needs the component to be
interactive, so a prerendered or static page would show no math, and the script would have to keep the DOM in step with
Blazor's own diffing.

### 3. Avalonia lays out the structure with its own font

`FormulaLayout` measures each part with the viewer's font and places it by simplified TeX rules: scripts smaller and
shifted, fractions and delimiters about the math axis, delimiters scaled to their content, limits below and above large
operators in a formula shown on its own. The vertical extent of each glyph's ink is estimated from the kind of
character (`GlyphInk`: x-height for most lowercase letters, cap height for capitals and digits, descenders, brackets,
accent marks), since the text engine gives no ink bounds; text fonts differ little enough in these proportions.

An inline formula is an embedded control whose bottom is the formula's baseline: the text engine sets an embedded
control's bottom on the line's baseline and makes the line taller above it only. The part below the baseline is drawn
outside the control, and the text block keeps that room as bottom padding.

## Consequences

- Both viewers render every formula part; coverage tests compare what each drew with the part types.
- The two viewers do not draw a formula identically: the browser uses a math font and its own rules, the Avalonia viewer
  the text font and estimated ink. The structure is the same.
- The Blazor README's statement that no markup is built from a string now names this exception, and a test enforces it.
- Delimiters and large operators grow in the browser only with a font that has math tables; the viewer names a few
  (`--pmd-math-font`), and a page without one shows them at text size.
- In Avalonia, a formula reaching deep below the baseline in the middle of a paragraph can overlap the line below; the
  last line's depth is reserved.
- Revisit the ink estimate if a later Avalonia gives glyph bounds or outlines on every platform, and the markup
  exception if Blazor learns the MathML namespace.

## Alternatives considered

- **Lay out in the core and draw shapes in both viewers, as for diagrams.** The most uniform result, but the core has no
  font metrics, so it would guess every glyph's width as well as its height, or need a bundled math font.
- **Blazor renders MathML, Avalonia shows the source.** Least work, but it breaks the rule that both viewers render
  every node.
- **Blazor lays out too, as SVG.** Blazor handles SVG, but laying out in .NET for the browser has the same missing
  metrics as laying out in the core.
