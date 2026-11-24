using Polhem.Markdown.Highlighting;
using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Highlighting;

[Collection(ScalingCollection.Name)]
public class CodeHighlighterTests
{
    private static readonly string[] s_languages =
        ["cs", "json", "sql", "bash", "powershell", "js", "ts", "python", "xml", "html", "razor", "yaml", "css"];

    /// <summary>Each token as <c>Kind:text</c>, in order.</summary>
    private static List<string> Tokens(string language, string code) =>
        CodeHighlighter.Tokenize(language, code).Select(token => token.Kind + ":" + code.Substring(token.Start, token.Length)).ToList();

    [Fact(DisplayName = "C#: keywords, strings of every form, comments, numbers and constants")]
    public void Tokenize_CSharp()
    {
        const string Code = "// note\nvar s = $\"a{b}\" + @\"c\\\" + \"\"\"raw\"\"\"; /* x */ if (n > 1.5m) return null;";

        Assert.Equal(
            ["Comment:// note", "Keyword:var", "String:$\"a{b}\"", "String:@\"c\\\"", "String:\"\"\"raw\"\"\"", "Comment:/* x */",
             "Keyword:if", "Constant:1.5m", "Keyword:return", "Constant:null"],
            Tokens("cs", Code));
    }

    [Fact(DisplayName = "JSON: keys are properties, values strings or constants")]
    public void Tokenize_Json()
    {
        Assert.Equal(
            ["Property:\"name\"", "String:\"pen\"", "Property:\"qty\"", "Constant:2", "Property:\"ok\"", "Constant:true", "Constant:null"],
            Tokens("json", "{ \"name\": \"pen\", \"qty\" : 2, \"ok\": true, \"x\": [null] }").Where(token => token != "Property:\"x\"").ToList());
    }

    [Fact(DisplayName = "SQL: keywords in any case, doubled quotes inside strings, line and block comments")]
    public void Tokenize_Sql()
    {
        Assert.Equal(
            ["Keyword:SELECT", "Keyword:from", "Keyword:Where", "String:'O''Brien'", "Comment:-- who", "Comment:/* end */"],
            Tokens("sql", "SELECT name from t Where n = 'O''Brien' -- who\n/* end */"));
    }

    [Fact(DisplayName = "Shell: comments, strings, variables and keywords")]
    public void Tokenize_Shell()
    {
        Assert.Equal(
            ["Comment:# build", "Keyword:if", "String:\"$HOME\"", "Keyword:then", "Property:$PATH", "Property:${NAME}", "Keyword:fi"],
            Tokens("bash", "# build\nif [ -d \"$HOME\" ]; then echo $PATH ${NAME}; fi"));
    }

    [Fact(DisplayName = "PowerShell: variables, constants, operators and keywords in any case, both comment forms")]
    public void Tokenize_PowerShell()
    {
        Assert.Equal(
            ["Comment:<# doc #>", "Keyword:If", "Property:$count", "Keyword:-gt", "Constant:3", "Property:$x", "Constant:$true", "Comment:# done"],
            Tokens("powershell", "<# doc #>\nIf ($count -gt 3) { $x = $true } # done"));
    }

    [Fact(DisplayName = "JavaScript and TypeScript: template strings span lines, keywords and constants")]
    public void Tokenize_JavaScript()
    {
        Assert.Equal(
            ["Keyword:const", "String:`a\n${b}`", "Keyword:interface", "Keyword:number", "Constant:undefined"],
            Tokens("ts", "const t = `a\n${b}`; interface P { n: number } let u = undefined;").Where(token => !token.StartsWith("Keyword:let", StringComparison.Ordinal)).ToList());
    }

    [Fact(DisplayName = "Python: triple-quoted, prefixed and raw strings, comments, keywords and constants")]
    public void Tokenize_Python()
    {
        Assert.Equal(
            ["Keyword:def", "String:\"\"\"doc\n\"\"\"", "Keyword:return", "String:f\"{x}\"", "String:r\"\\d\"", "Constant:None", "Comment:# end"],
            Tokens("python", "def f():\n    \"\"\"doc\n\"\"\"\n    return f\"{x}\", r\"\\d\", None # end"));
    }

    [Fact(DisplayName = "XML: tags, attributes, values, comments and entities")]
    public void Tokenize_Xml()
    {
        Assert.Equal(
            ["Comment:<!-- c -->", "Tag:Project", "Property:Sdk", "String:\"Web\"", "Tag:a", "Constant:&amp;", "Tag:a", "Tag:Project"],
            Tokens("xml", "<!-- c --><Project Sdk=\"Web\"><a>x &amp; y</a></Project>"));
    }

    [Fact(DisplayName = "HTML: the bodies of script and style elements are read as JavaScript and CSS")]
    public void Tokenize_HtmlScriptAndStyle()
    {
        Assert.Equal(
            ["Tag:script", "Keyword:let", "String:'x'", "Tag:script", "Tag:style", "Tag:p", "Property:color", "Tag:style"],
            Tokens("html", "<script>let a = 'x';</script><style>p { color: red }</style>"));
    }

    [Fact(DisplayName = "Razor: directives, C# blocks and conditions, implicit expressions and comments")]
    public void Tokenize_Razor()
    {
        const string Code = "@page \"/x\"\n@* note *@\n<p class=\"@Css\">@Model.Name</p>\n@if (n > 1) { <b>y</b> }\n@code { int n = 2; }";

        Assert.Equal(
            ["Keyword:@page", "String:\"/x\"", "Comment:@* note *@", "Tag:p", "Property:class", "String:\"", "Property:@Css", "String:\"",
             "Property:@Model.Name", "Tag:p", "Keyword:@if", "Constant:1", "Tag:b", "Tag:b", "Keyword:@code", "Keyword:int", "Constant:2"],
            Tokens("razor", Code));
    }

    [Fact(DisplayName = "Razor: an e-mail address and an escaped at sign stay text")]
    public void Tokenize_RazorEmailAndEscape()
    {
        Assert.Empty(Tokens("razor", "mail me@example.com or @@home"));
    }

    [Fact(DisplayName = "YAML: keys, strings, constants, comments, document markers, and block scalars left plain")]
    public void Tokenize_Yaml()
    {
        const string Code = "---\nname: \"app\" # n\nreplicas: 3\nenabled: true\nitems:\n  - id: 1\nscript: |\n  echo: no\n  if: yes\nafter: ~";

        Assert.Equal(
            ["Keyword:---", "Property:name", "String:\"app\"", "Comment:# n", "Property:replicas", "Constant:3", "Property:enabled", "Constant:true",
             "Property:items", "Property:id", "Constant:1", "Property:script", "Property:after", "Constant:~"],
            Tokens("yaml", Code));
    }

    [Fact(DisplayName = "CSS: selectors, at-rules, properties, numbers, colors, strings, comments and !important")]
    public void Tokenize_Css()
    {
        Assert.Equal(
            ["Comment:/* c */", "Keyword:@media", "Tag:.btn", "Tag:#main", "Property:margin", "Constant:4px", "Constant:.5em",
             "Property:color", "Constant:#fff", "Keyword:!important", "Property:content", "String:\"x\""],
            Tokens("css", "/* c */ @media screen { .btn:hover, #main { margin: 4px .5em; color: #fff !important; content: \"x\" } }")
                .Where(token => token != "Tag:screen").ToList());
    }

    [Theory(DisplayName = "In XML, XAML and Avalonia XAML a Style or Script element is markup like any other")]
    [InlineData("xml")]
    [InlineData("xaml")]
    [InlineData("axaml")]
    public void Tokenize_XmlStyleElement_IsMarkup(string language)
    {
        Assert.Equal(
            ["Tag:Style", "Property:Selector", "String:\"Button\"", "Tag:Setter", "Property:Property", "String:\"Background\"",
             "Property:Value", "String:\"Red\"", "Tag:Style"],
            Tokens(language, "<Style Selector=\"Button\"><Setter Property=\"Background\" Value=\"Red\"/></Style>"));
    }

    [Fact(DisplayName = "YAML with Windows line endings reads keys, list items and block scalars the same way")]
    public void Tokenize_YamlWithCrLf()
    {
        var unix = Tokens("yaml", "parent:\n  - child: 1\nscript: |\n  a: b\n\n  c: d\nafter: true");
        var windows = Tokens("yaml", "parent:\r\n  - child: 1\r\nscript: |\r\n  a: b\r\n\r\n  c: d\r\nafter: true");

        Assert.Equal(unix, windows);
        Assert.Contains("Property:parent", windows);
        Assert.DoesNotContain("Property:c", windows);
    }

    [Fact(DisplayName = "A shell variable in braces that is not closed ends at the end of its line")]
    public void Tokenize_UnclosedShellBrace_EndsAtLine()
    {
        Assert.Equal(["Property:${A", "Property:$B"], Tokens("bash", "${A\necho $B"));
    }

    [Theory(DisplayName = "The fence's language matches its usual names in any case, and only its first word counts")]
    [InlineData("C#")]
    [InlineData("CSharp")]
    [InlineData("cs title=\"Program.cs\"")]
    [InlineData("{.cs}")]
    public void Tokenize_LanguageNames(string language)
    {
        Assert.Equal(["Keyword:var"], Tokens(language, "var x;"));
    }

    [Theory(DisplayName = "A language the highlighter does not know, or none, leaves the code plain")]
    [InlineData("")]
    [InlineData("text")]
    [InlineData("rust")]
    public void Tokenize_UnknownLanguage_IsPlain(string language)
    {
        Assert.Empty(CodeHighlighter.Tokenize(language, "fn main() { let x = \"s\"; } // c"));
    }

    [Fact(DisplayName = "A code block in the model carries its tokens; an indented one has none")]
    public void Build_CodeBlock_CarriesTokens()
    {
        var document = Parse("```cs\nvar x = 1;\n```\n\n    var y = 2;");

        Assert.Equal(2, ((CodeBlockNode)document.Blocks[0]).Tokens.Count);
        Assert.Empty(((CodeBlockNode)document.Blocks[1]).Tokens);
    }

    [Theory(DisplayName = "Code cut off anywhere, as while a reply arrives, gives ordered tokens inside the code")]
    [MemberData(nameof(Cuts))]
    public void Tokenize_TruncatedCode_StaysWellFormed(string language, string code)
    {
        for (var length = 0; length <= code.Length; length++)
        {
            AssertWellFormed(CodeHighlighter.Tokenize(language, code[..length]), length);
        }
    }

    public static TheoryData<string, string> Cuts() => new()
    {
        { "cs", "var s = $@\"a\"\"b\" + \"\"\"r\"\"\" /* c */ // d\n'x'" },
        { "python", "x = r'''a\\''' + f\"{b}\" # c" },
        { "razor", "@code { var s = \"}\"; } <p a=\"@x\">@(y) @* c *@</p>" },
        { "html", "<!-- c --><script>let a = `b`</script><style>p{color:#fff}</style>&amp;" },
        { "yaml", "a: 'b' # c\nd: |\n  e\nf: 1" },
        { "css", "@media x { .a { b: 1px !important; c: \"d\" } } /* e */" },
        { "powershell", "<# a #> $b = \"c`\"d\" -eq $true" },
    };

    [Theory(DisplayName = "Adversarial code is read in linear time")]
    [Trait("Category", Scaling.Category)]
    [MemberData(nameof(Adversarial))]
    public void Tokenize_AdversarialCode_IsLinear(string language, string unit)
    {
        string Code(int length) => string.Concat(Enumerable.Repeat(unit, Math.Max(1, length / unit.Length)));

        var code = Code(100_000);
        AssertWellFormed(CodeHighlighter.Tokenize(language, code), code.Length);
        Scaling.AssertLinear(Code, text => CodeHighlighter.Tokenize(language, text), 10_000, 800_000, $"{language} with {unit.Replace("\n", "\\n", StringComparison.Ordinal)}");
    }

    public static TheoryData<string, string> Adversarial()
    {
        var data = new TheoryData<string, string>();
        foreach (var language in s_languages)
        {
            foreach (var unit in new[] { "\"", "'", "/*", "<", "@{", "@(", "<script>", "<style>", "a: ", "$", "${\n", "-- ", "{", "<!--", "@*", "x\n", "`", "a{b:", "${" + new string('a', 60) + "\n", "${ " + new string('x', 19) + "\n" })
            {
                data.Add(language, unit);
            }
        }
        return data;
    }

    private static void AssertWellFormed(IReadOnlyList<CodeToken> tokens, int length)
    {
        var previousEnd = 0;
        foreach (var token in tokens)
        {
            Assert.True(token.Length > 0);
            Assert.True(token.Start >= previousEnd, $"Token at {token.Start} overlaps the one ending at {previousEnd}.");
            Assert.True(token.End <= length, $"Token ending at {token.End} passes the end at {length}.");
            previousEnd = token.End;
        }
    }
}
