namespace Polhem.Markdown.Formulas;

/// <summary>The LaTeX commands that stand for one symbol, and the delimiters, with how each is set.</summary>
internal static class LatexSymbols
{
    private static readonly Dictionary<string, FormulaSymbol> s_commands = Build();

    private static readonly Dictionary<string, string> s_delimiters = new()
    {
        ["langle"] = "⟨", ["rangle"] = "⟩", ["lfloor"] = "⌊", ["rfloor"] = "⌋", ["lceil"] = "⌈",
        ["rceil"] = "⌉", ["lbrace"] = "{", ["rbrace"] = "}", ["vert"] = "|", ["Vert"] = "‖", ["lvert"] = "|",
        ["rvert"] = "|", ["lVert"] = "‖", ["rVert"] = "‖", ["{"] = "{", ["}"] = "}", ["|"] = "‖",
    };

    /// <summary>The symbol a command stands for, or <see langword="null"/> when it stands for none.</summary>
    public static FormulaSymbol? Command(string name) => s_commands.GetValueOrDefault(name);

    /// <summary>The delimiter a command names after <c>\left</c> or <c>\right</c>, or <see langword="null"/>.</summary>
    public static string? Delimiter(string name) => s_delimiters.GetValueOrDefault(name);

    private static Dictionary<string, FormulaSymbol> Build()
    {
        var symbols = new Dictionary<string, FormulaSymbol>();
        void Add(FormulaSymbolKind kind, string pairs)
        {
            // Each entry is `name=symbol`, separated by spaces.
            foreach (var pair in pairs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                symbols[pair[..equals]] = new FormulaSymbol(pair[(equals + 1)..], kind);
            }
        }

        Add(FormulaSymbolKind.Identifier,
            "alpha=α beta=β gamma=γ delta=δ epsilon=ϵ varepsilon=ε zeta=ζ eta=η theta=θ vartheta=ϑ iota=ι kappa=κ " +
            "lambda=λ mu=μ nu=ν xi=ξ pi=π varpi=ϖ rho=ρ varrho=ϱ sigma=σ varsigma=ς tau=τ upsilon=υ phi=ϕ varphi=φ " +
            "chi=χ psi=ψ omega=ω ell=ℓ");
        Add(FormulaSymbolKind.Name,
            "Gamma=Γ Delta=Δ Theta=Θ Lambda=Λ Xi=Ξ Pi=Π Sigma=Σ Upsilon=Υ Phi=Φ Psi=Ψ Omega=Ω infty=∞ partial=∂ " +
            "nabla=∇ forall=∀ exists=∃ nexists=∄ emptyset=∅ varnothing=∅ hbar=ℏ aleph=ℵ Re=ℜ Im=ℑ angle=∠ " +
            "triangle=△ top=⊤ bot=⊥ " +
            "sin=sin cos=cos tan=tan cot=cot sec=sec csc=csc arcsin=arcsin arccos=arccos arctan=arctan sinh=sinh " +
            "cosh=cosh tanh=tanh coth=coth log=log ln=ln lg=lg exp=exp det=det dim=dim ker=ker arg=arg deg=deg " +
            "gcd=gcd hom=hom Pr=Pr");
        Add(FormulaSymbolKind.LimitName, "lim=lim limsup=lim sup liminf=lim inf max=max min=min sup=sup inf=inf");
        Add(FormulaSymbolKind.LargeOperator, "sum=∑ prod=∏ coprod=∐ bigcup=⋃ bigcap=⋂ bigoplus=⨁ bigotimes=⨂ bigvee=⋁ bigwedge=⋀");
        Add(FormulaSymbolKind.Integral, "int=∫ iint=∬ iiint=∭ oint=∮");
        Add(FormulaSymbolKind.Operator,
            "times=× div=÷ cdot=⋅ pm=± mp=∓ ast=∗ star=⋆ circ=∘ bullet=∙ oplus=⊕ ominus=⊖ otimes=⊗ odot=⊙ cap=∩ " +
            "cup=∪ setminus=∖ wedge=∧ land=∧ vee=∨ lor=∨ neg=¬ lnot=¬ " +
            "leq=≤ le=≤ geq=≥ ge=≥ neq=≠ ne=≠ approx=≈ equiv=≡ sim=∼ simeq=≃ cong=≅ propto=∝ in=∈ notin=∉ ni=∋ " +
            "subset=⊂ supset=⊃ subseteq=⊆ supseteq=⊇ ll=≪ gg=≫ perp=⊥ parallel=∥ mid=∣ prec=≺ succ=≻ " +
            "to=→ rightarrow=→ leftarrow=← gets=← leftrightarrow=↔ Rightarrow=⇒ Leftarrow=⇐ Leftrightarrow=⇔ " +
            "iff=⟺ implies=⟹ impliedby=⟸ mapsto=↦ uparrow=↑ downarrow=↓ longrightarrow=⟶ longleftarrow=⟵ " +
            "longmapsto=⟼ " +
            "ldots=… dots=… cdots=⋯ vdots=⋮ ddots=⋱ colon=: prime=′ " +
            "langle=⟨ rangle=⟩ lfloor=⌊ rfloor=⌋ lceil=⌈ rceil=⌉ lbrace={ rbrace=} vert=| Vert=‖");
        return symbols;
    }
}
