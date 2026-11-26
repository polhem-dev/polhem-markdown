using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Markdown.Highlighting;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

internal sealed partial class MarkdownRenderTree
{
    private const string CopyIcon =
        "M8 2h9a2 2 0 0 1 2 2v11h-2V4H8V2zM5 6h9a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2zm0 2v12h9V8H5z";

    private const string DoneIcon = "M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z";

    private void CodeBlock(CodeBlockNode code)
    {
        var copied = _viewer.IsCopied(code);

        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-code");

        _builder.OpenElement(2, "div");
        _builder.AddAttribute(3, ClassAttribute, "pmd-code-header");
        _builder.OpenElement(4, "span");
        _builder.AddAttribute(5, ClassAttribute, "pmd-code-lang");
        _builder.AddContent(6, code.Language);
        _builder.CloseElement();

        _builder.OpenElement(7, ButtonElement);
        _builder.AddAttribute(8, TypeAttribute, ButtonElement);
        _builder.AddAttribute(9, ClassAttribute, "pmd-copy");
        _builder.AddAttribute(10, "title", _viewer.CopyCodeLabel);
        _builder.AddAttribute(11, "aria-label", _viewer.CopyCodeLabel);
        _builder.AddAttribute(12, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.CopyAsync(code)));
        _builder.OpenElement(13, "svg");
        _builder.AddAttribute(14, "viewBox", "0 0 24 24");
        _builder.AddAttribute(15, "aria-hidden", "true");
        _builder.OpenElement(16, "path");
        _builder.AddAttribute(17, "d", copied ? DoneIcon : CopyIcon);
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();

        _builder.OpenElement(18, "pre");
        _builder.OpenElement(19, "code");
        CodeText(20, code);
        _builder.CloseElement();
        _builder.CloseElement();

        _builder.CloseElement();
    }

    /// <summary>
    /// Writes the code as text, with each highlighted token in a span whose class names its kind; the stylesheet colors
    /// the kinds through the <c>--pmd-code-*</c> variables.
    /// </summary>
    private void CodeText(int sequence, CodeBlockNode code)
    {
        _builder.OpenRegion(sequence);
        var position = 0;
        foreach (var token in code.Tokens)
        {
            if (token.Start > position)
            {
                _builder.AddContent(0, code.Code[position..token.Start]);
            }
            _builder.OpenElement(1, "span");
            _builder.AddAttribute(2, ClassAttribute, TokenClass(token.Kind));
            _builder.AddContent(3, code.Code.Substring(token.Start, token.Length));
            _builder.CloseElement();
            position = token.End;
        }
        if (position < code.Code.Length)
        {
            _builder.AddContent(4, code.Code[position..]);
        }
        _builder.CloseRegion();
    }

    private static string TokenClass(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => "pmd-tk-keyword",
        CodeTokenKind.String => "pmd-tk-string",
        CodeTokenKind.Comment => "pmd-tk-comment",
        CodeTokenKind.Constant => "pmd-tk-constant",
        CodeTokenKind.Tag => "pmd-tk-tag",
        _ => "pmd-tk-property",
    };

    private void Alert(AlertNode alert)
    {
        var kind = alert.Kind.ToString().ToLowerInvariant();
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-alert pmd-alert-" + kind);

        // The Avalonia viewer shows an alert as a group named by its kind; a note is the role that comes nearest.
        _builder.AddAttribute(2, "role", "note");
        _builder.OpenElement(3, "p");
        _builder.AddAttribute(4, ClassAttribute, "pmd-alert-title");
        _builder.OpenElement(5, "svg");
        _builder.AddAttribute(6, "viewBox", "0 0 16 16");
        _builder.AddAttribute(7, "aria-hidden", "true");
        _builder.OpenElement(8, "path");
        _builder.AddAttribute(9, "fill-rule", "evenodd");
        _builder.AddAttribute(10, "d", AlertGlyphs.IconPath(alert.Kind));
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.AddContent(11, AlertGlyphs.Title(alert.Kind));
        _builder.CloseElement();
        Blocks(12, alert.Blocks);
        _builder.CloseElement();
    }
}
