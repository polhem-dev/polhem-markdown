namespace MarkdownGallery;

/// <summary>A reply that uses every kind of node the viewers draw, and the cases the safety policy handles.</summary>
internal static class SampleText
{
    public const string Markdown = """
        # Order form draft

        I drafted the **order** form from your document. It has *three* sections and uses `ft_order` as its table.
        See the [Markdig project](https://github.com/xoofx/markdig) for the syntax, or open the
        [order form](polhem:form/order?id=7) directly.

        請確認**「必填」**欄位是否正確。

        ## Fields

        1. Order number, read only
        2. Customer, a lookup to `ft_customer`
           - shows the customer name
           - fills in the address
        3. Order date

        Still to do:

        - [x] Add the ~~customer code~~ customer lookup
        - [ ] Check the rounding of the amount

        > The quantity and the unit price are multiplied into the amount,
        > which is rounded to two decimals.

        | Field      | Type    | Required |
        |:-----------|:-------:|---------:|
        | order_no   | String  | yes      |
        | order_date | Date    | yes      |
        | amount     | Decimal | no       |

        ```csharp
        var amount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero); // a long line that scrolls sideways instead of wrapping
        ```

        Raw HTML such as <b>this</b> and <script>alert(1)</script> shows as text, a [script link](javascript:alert(1))
        cannot be clicked, and an image shows its alternative text: ![a logo](https://example.com/logo.png).
        A line can end with a hard break\
        like this one.

        ---

        That's all for now.
        """;
}
