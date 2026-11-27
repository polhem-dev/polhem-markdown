using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Markdown.Model;
using Polhem.Markdown.Output;

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
        CodeBlockActions(7, code);

        _builder.OpenElement(8, ButtonElement);
        _builder.AddAttribute(9, TypeAttribute, ButtonElement);
        _builder.AddAttribute(10, ClassAttribute, "pmd-copy");
        _builder.AddAttribute(11, "title", _viewer.CopyCodeLabel);
        _builder.AddAttribute(12, "aria-label", _viewer.CopyCodeLabel);
        _builder.AddAttribute(13, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.CopyAsync(code)));
        _builder.OpenElement(14, "svg");
        _builder.AddAttribute(15, "viewBox", "0 0 24 24");
        _builder.AddAttribute(16, "aria-hidden", "true");
        _builder.OpenElement(17, "path");
        _builder.AddAttribute(18, "d", copied ? DoneIcon : CopyIcon);
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();

        _builder.OpenElement(19, "pre");
        _builder.OpenElement(20, "code");
        CodeText(21, code);
        _builder.CloseElement();
        _builder.CloseElement();

        _builder.CloseElement();
    }

    /// <summary>
    /// Writes a button for each of the host's actions that <paramref name="code"/> shows, before the copy button: its
    /// icon, or its label as text when it has none.
    /// </summary>
    private void CodeBlockActions(int sequence, CodeBlockNode code)
    {
        _builder.OpenRegion(sequence);
        var block = new MarkdownCodeBlock(code.Code, code.Language);
        foreach (var action in _viewer.CodeBlockActions ?? [])
        {
            if (!action.IsShownOn(block))
            {
                continue;
            }
            _builder.OpenElement(0, ButtonElement);
            _builder.AddAttribute(1, TypeAttribute, ButtonElement);
            _builder.AddAttribute(2, ClassAttribute, "pmd-code-action");
            _builder.AddAttribute(3, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => action.InvokeAsync(block)));
            if (action.IconPath is { } icon)
            {
                _builder.AddAttribute(4, "title", action.Label);
                _builder.AddAttribute(5, "aria-label", action.Label);
                _builder.OpenElement(6, "svg");
                _builder.AddAttribute(7, "viewBox", "0 0 24 24");
                _builder.AddAttribute(8, "aria-hidden", "true");
                _builder.OpenElement(9, "path");
                _builder.AddAttribute(10, "d", icon);
                _builder.CloseElement();
                _builder.CloseElement();
            }
            else
            {
                _builder.AddContent(11, action.Label);
            }
            _builder.CloseElement();
        }
        _builder.CloseRegion();
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
            _builder.AddAttribute(2, ClassAttribute, ViewerMarkup.TokenClass(token.Kind));
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

    private void Alert(AlertNode alert)
    {
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, ViewerMarkup.AlertClass(alert.Kind));

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
