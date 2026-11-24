namespace MarkdownGallery;

/// <summary>A reply that uses every kind of node the viewers draw, and the cases the safety policy handles.</summary>
internal static class SampleText
{
    public const string Markdown = """
        # Order form draft

        [![build passing](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/polhem-dev/polhem-markdown)

        I drafted the **order** form from your document. It has *three* sections and uses `ft_order` as its table.
        See the [Markdig project](https://github.com/xoofx/markdig) for the syntax, or open the
        [order form](polhem:form/order?id=7) directly.

        請確認**「必填」**欄位是否正確。The [fields](#fields) are listed below.

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
        > which is rounded to two decimals.[^rounding]

        > [!NOTE]
        > The order number is assigned when the form is saved.

        > [!TIP]
        > Press F4 in the customer field to search.

        > [!IMPORTANT]
        > Every order needs a customer.

        > [!WARNING]
        > Changing the unit price recalculates every line.

        > [!CAUTION]
        > Deleting an order cannot be undone.

        | Field      | Type    | Required |
        |:-----------|:-------:|---------:|
        | order_no   | String  | yes      |
        | order_date | Date    | yes      |
        | amount     | Decimal | no       |

        ```csharp
        var amount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero); // a long line that scrolls sideways instead of wrapping
        ```

        The form definition it produces:

        ```json
        { "table": "ft_order", "readOnly": ["order_no"], "rounding": 2, "audit": true }
        ```

        The order goes through these steps:

        ```mermaid
        flowchart TD
            A([New order]) --> B[Check the customer]
            B --> C{Credit OK?}
            C -->|yes| D[Reserve stock]
            C -->|no| E[Hold]
            D --> F((Done))
        ```

        ```mermaid
        flowchart LR
            A([訂單建立]) --> B{客戶信用<br/>是否足夠}
            B -->|是| C[保留庫存]
            B -.->|否| D{{暫停}}
            C ==> E((完成))
            D -->|補件後| B
        ```

        ```mermaid
        sequenceDiagram
            participant U as User
            participant F as Order form
            participant S as Server
            U->>F: Save
            F->>S: Validate the order
            Note over F,S: The amount is recalculated on the server
            S-->>F: Order number 7
            F->>F: Refresh the fields
            F-->>U: Saved
            Note right of U: Done
        ```

        A diagram type the viewers do not draw shows as code:

        ```mermaid
        pie title Orders by region
            "North" : 42
            "South" : 58
        ```

        <div align="center">

        The HTML README files use works: <b>bold</b>, x<sup>2</sup>, H<sub>2</sub>O, <kbd>Ctrl</kbd> + <kbd>C</kbd>.

        </div>

        <details>
        <summary>Click to open</summary>

        A section the reader opens, with **Markdown** inside.

        </details>

        Any other HTML is dropped: <span onclick="alert(1)">this text</span> stays and <script>alert(1)</script> goes, a [script link](javascript:alert(1))
        cannot be clicked, and an image the viewer cannot load shows its alternative text: ![a logo](https://example.com/logo.png).
        A line can end with a hard break\
        like this one.

        ---

        That's all for now. :wave:

        [^rounding]: Halves round away from zero, so 2.345 becomes 2.35. See the [fields](#fields) for the columns.
        """;
}
