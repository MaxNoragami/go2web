using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using System.Text.RegularExpressions;

namespace go2web.Rendering;

public class HtmlRenderer
{
    private static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "SCRIPT", "STYLE", "NOSCRIPT", "SVG", "META", "LINK", "HEAD", "IFRAME", "TITLE"
    };

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "DIV", "P", "H1", "H2", "H3", "H4", "H5", "H6",
        "UL", "OL", "LI", "TR", "BR", "HEADER",
        "FOOTER", "MAIN", "SECTION", "ARTICLE", "ASIDE", "NAV", "BODY", "HR"
    };

    public IEnumerable<IRenderable> Render(string html)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);
        
        var renderables = new List<IRenderable>();
        var sb = new StringBuilder();

        void FlushText()
        {
            string text = sb.ToString();
            text = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
            if (!string.IsNullOrEmpty(text))
            {
                renderables.Add(new Markup(text + "\n"));
            }
            sb.Clear();
        }

        void WalkNode(INode node, bool preserveWhitespace)
        {
            if (node is IElement element && element.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                FlushText();
                renderables.Add(ParseTable(element));
                return;
            }

            RenderNodeToBuilder(node, sb, preserveWhitespace, WalkNode);
        }

        if (document.Body != null)
        {
            WalkNode(document.Body, false);
        }
        
        FlushText();
        
        return renderables;
    }

    private Table ParseTable(IElement tableElement)
    {
        var table = new Table().Border(TableBorder.Rounded);


        var thead = tableElement.QuerySelector("thead");
        var headerCells = thead?.QuerySelectorAll("th, td") ?? tableElement.QuerySelectorAll("tr").FirstOrDefault()?.QuerySelectorAll("th, td");
        
        if (headerCells != null)
        {
            foreach (var th in headerCells)
            {
                table.AddColumn(new TableColumn(new Markup(RenderNodeToString(th, false).Trim())));
            }
        }

        // Find rows
        var tbody = tableElement.QuerySelector("tbody") ?? tableElement;
        var rows = tbody.QuerySelectorAll("tr");

        bool isFirstRow = true;
        foreach (var tr in rows)
        {
            if (isFirstRow && thead == null && tr.QuerySelector("th") != null) 
            {
                isFirstRow = false;
                continue;
            }
            isFirstRow = false;

            var cells = tr.QuerySelectorAll("td, th");
            if (cells.Length == 0) continue;

            // Ensure table has enough columns
            while (table.Columns.Count < cells.Length)
            {
                table.AddColumn("");
            }

            var rowData = new List<IRenderable>();
            foreach (var td in cells)
            {
                var cellText = RenderNodeToString(td, false).Trim();
                if (string.IsNullOrEmpty(cellText)) cellText = " ";
                rowData.Add(new Markup(cellText));
            }
            
            // Pad if necessary
            while (rowData.Count < table.Columns.Count)
            {
                rowData.Add(new Markup(" "));
            }

            table.AddRow(rowData);
        }

        return table;
    }

    private string RenderNodeToString(INode node, bool preserveWhitespace)
    {
        var tempSb = new StringBuilder();
        void Walk(INode n, bool pw)
        {
            if (n is IElement el && el.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                tempSb.Append(Markup.Escape(n.TextContent));
                return;
            }
            RenderNodeToBuilder(n, tempSb, pw, Walk);
        }
        Walk(node, preserveWhitespace);
        return tempSb.ToString();
    }

    private void RenderNodeToBuilder(INode node, StringBuilder sb, bool preserveWhitespace, Action<INode, bool> walkChildren)
    {
        if (node is IText textNode)
        {
            if (preserveWhitespace)
            {
                sb.Append(Markup.Escape(textNode.Text));
            }
            else
            {
                string text = Regex.Replace(textNode.Text, @"\s+", " ");
                if (!string.IsNullOrWhiteSpace(text) || text == " ")
                {
                    sb.Append(Markup.Escape(text));
                }
            }
            return;
        }

        if (node is IElement element)
        {
            string tagName = element.TagName;

            if (IgnoredTags.Contains(tagName))
                return;

            bool isBlock = BlockTags.Contains(tagName);
            bool isPre = tagName.Equals("PRE", StringComparison.OrdinalIgnoreCase);
            
            if (isBlock && tagName != "BODY") 
                sb.AppendLine();

            if (tagName == "LI")
                sb.Append("• ");

            string? formatStyle = GetSpectreStyle(tagName);
            if (formatStyle != null) 
            {
                sb.Append($"[{formatStyle}]");
            }

            string? href = tagName.Equals("A", StringComparison.OrdinalIgnoreCase) ? element.GetAttribute("href") : null;
            if (href != null && href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append($"[link={Markup.Escape(href)}]");
            }

            foreach (var child in element.ChildNodes)
            {
                walkChildren(child, preserveWhitespace || isPre);
            }

            if (href != null && href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("[/]");
            }

            if (formatStyle != null) 
            {
                sb.Append("[/]");
            }

            if (isBlock && tagName != "BODY") 
                sb.AppendLine();
        }
    }

    private string? GetSpectreStyle(string tagName)
    {
        return tagName.ToUpperInvariant() switch
        {
            "H1" or "H2" or "H3" => "bold underline white",
            "H4" or "H5" or "H6" or "B" or "STRONG" => "bold white",
            "I" or "EM" => "italic",
            "A" => "blue",
            _ => null
        };
    }
}
