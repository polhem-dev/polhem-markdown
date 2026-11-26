using Bunit;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>
/// What the viewer's HTML tells an assistive technology, checked against the roles the Avalonia viewer shows the same
/// nodes with (polhem-dev/polhem-markdown#70).
/// </summary>
public class AccessibilityTests : BunitContext
{
    public AccessibilityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact(DisplayName = "Every node type of the model has a role, the same one the Avalonia viewer shows it with")]
    public void Roles_CoverEveryNodeType()
    {
        Assert.Equal(ModelNodeTypes.All.OrderBy(type => type.Name), AccessibleRoles.ByNodeType.Keys.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "The viewer writes each node type as an element of its role")]
    public void Roles_EachNodeTypeWrittenWithItsRole()
    {
        var missing = new List<string>();
        foreach (var (type, (markdown, role)) in AccessibleRoles.ByNodeType)
        {
            if (role == AccessibleRole.None)
            {
                continue;
            }
            var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, markdown));
            if (!HasRole(viewer, role))
            {
                missing.Add(type.Name + " as " + role);
            }
        }
        Assert.Empty(missing);
    }

    [Fact(DisplayName = "An alert is a note, so it is set apart from the text as the Avalonia viewer's group is")]
    public void Alert_IsNote()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, "> [!TIP]\n> Try this."));

        Assert.Equal("note", viewer.Find(".pmd-alert").GetAttribute("role"));
    }

    private static bool HasRole(IRenderedComponent<MarkdownViewer> viewer, AccessibleRole role) => role switch
    {
        AccessibleRole.Text => viewer.FindAll("p").Count > 0,
        AccessibleRole.Heading => viewer.FindAll("h1, h2, h3, h4, h5, h6").Count > 0,
        AccessibleRole.Link => viewer.FindAll("a[href]").Count > 0,
        AccessibleRole.List => viewer.FindAll("ul, ol").Count > 0,
        AccessibleRole.ListItem => viewer.FindAll("li").Count > 0,
        AccessibleRole.Table => viewer.FindAll("table").Count > 0,
        AccessibleRole.HeaderCell => viewer.FindAll("th").Count > 0,
        AccessibleRole.Cell => viewer.FindAll("td").Count > 0,
        AccessibleRole.Image => viewer.FindAll("img[alt]").Any(image => image.GetAttribute("alt")!.Length > 0),
        AccessibleRole.Group => viewer.FindAll("blockquote, [role=note]").Count > 0,
        AccessibleRole.Disclosure => viewer.FindAll("details > summary").Count > 0,
        AccessibleRole.Separator => viewer.FindAll("hr").Count > 0,
        AccessibleRole.Formula => viewer.FindAll("math").Count > 0,
        AccessibleRole.Code => viewer.FindAll("pre").Count > 0
            && viewer.FindAll("button[aria-label]").Any(button => button.GetAttribute("aria-label") == viewer.Instance.CopyCodeLabel),
        _ => false,
    };
}
