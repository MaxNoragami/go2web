using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using System.Text.RegularExpressions;

namespace go2web.Rendering;

// Class responsible for rendering HTML content into a format suitable for terminal display using Spectre.Console
public class HtmlRenderer
{
    // A set of HTML tags that should be ignored during rendering, as they typically do not contribute meaningful content to the rendered output
    private static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "SCRIPT", "STYLE", "NOSCRIPT", "SVG", "META", "LINK", "HEAD", "IFRAME", "TITLE"
    };

    // A set of HTML tags that are considered block-level elements, which typically should be rendered with line breaks before and after them to maintain proper formatting in the terminal output
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "DIV", "P", "H1", "H2", "H3", "H4", "H5", "H6",
        "UL", "OL", "LI", "TR", "BR", "HEADER",
        "FOOTER", "MAIN", "SECTION", "ARTICLE", "ASIDE", "NAV", "BODY", "HR"
    };

    // Renders the given HTML string into a collection of IRenderable objects that can be displayed in the terminal, using the provided base URI to resolve relative links and resources
    public IEnumerable<IRenderable> Render(string html, Uri baseUri)
    {
        // Parse the HTML content using AngleSharp to create a DOM representation of the document
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);
        
        // A list to hold the rendered output as IRenderable objects, and a StringBuilder to accumulate text content while traversing the DOM
        var renderables = new List<IRenderable>();
        var sb = new StringBuilder();

        // A helper method to flush the accumulated text in the StringBuilder into a Markup renderable and add it to the list of renderables, while also cleaning up whitespace and ensuring proper paragraph breaks
        void FlushText()
        {
            string text = sb.ToString();
            // Replace multiple line breaks with a single clean paragraph break.
            text = Regex.Replace(text, @"(\r?\n\s*){2,}", "\n\n");
            // Also clean up leading spaces at the start of a line
            text = Regex.Replace(text, @"\n[ \t]+", "\n");
            
            text = text.Trim();
            
            if (!string.IsNullOrEmpty(text))
            {
                renderables.Add(new Markup(text + "\n"));
            }
            sb.Clear();
        }

        // A recursive method to walk through the DOM nodes, render their content according to their tag types, and accumulate text in the StringBuilder or create renderables for tables and images as needed.
        void WalkNode(INode node, bool preserveWhitespace)
        {
            if (node is IElement element && element.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                FlushText();
                renderables.Add(ParseTable(element, baseUri));
                return;
            }

            RenderNodeToBuilder(node, sb, preserveWhitespace, baseUri, WalkNode);
        }

        if (document.Body != null)
        {
            WalkNode(document.Body, false);
        }
        
        FlushText();
        
        return renderables;
    }

    // A helper method to resolve a potentially relative URL against the base URI of the document, ensuring that links and resources are correctly identified and can be accessed when rendered in the terminal
    private string ResolveUrl(string url, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        try
        {
            if (Uri.TryCreate(baseUri, url, out var resolvedUri))
            {
                return resolvedUri.AbsoluteUri;
            }
        }
        catch { }
        return url;
    }

    // Parses an HTML table element and converts it into a Spectre.Console Table renderable, extracting headers and rows while ensuring proper formatting and handling of edge cases such as missing headers or irregular row structures
    private Table ParseTable(IElement tableElement, Uri baseUri)
    {
        var table = new Table().Border(TableBorder.Rounded);

        var thead = tableElement.QuerySelector("thead");
        var headerCells = thead?.QuerySelectorAll("th, td") ?? tableElement.QuerySelectorAll("tr").FirstOrDefault()?.QuerySelectorAll("th, td");
        
        var tbody = tableElement.QuerySelector("tbody") ?? tableElement;
        var rows = tbody.QuerySelectorAll("tr");

        int maxColumns = headerCells?.Length ?? 0;
        foreach (var tr in rows)
        {
            maxColumns = Math.Max(maxColumns, tr.QuerySelectorAll("td, th").Length);
        }

        if (maxColumns == 0)
        {
            table.AddColumn("");
            return table;
        }

        if (headerCells != null)
        {
            for (int i = 0; i < maxColumns; i++)
            {
                if (i < headerCells.Length)
                {
                    var text = RenderNodeToString(headerCells[i], false, baseUri).Trim();
                    if (string.IsNullOrEmpty(text)) text = " ";
                    table.AddColumn(new TableColumn(new Markup(text)));
                }
                else
                {
                    table.AddColumn("");
                }
            }
        }
        else
        {
            for (int i = 0; i < maxColumns; i++)
            {
                table.AddColumn("");
            }
        }

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

            var rowData = new List<IRenderable>();
            foreach (var td in cells)
            {
                var cellText = RenderNodeToString(td, false, baseUri).Trim();
                if (string.IsNullOrEmpty(cellText)) cellText = " ";
                rowData.Add(new Markup(cellText));
            }
            
            // Pad if necessary
            while (rowData.Count < maxColumns)
            {
                rowData.Add(new Markup(" "));
            }

            table.AddRow(rowData);
        }

        return table;
    }

    // A helper method to render a DOM node and its children into a string representation, applying appropriate formatting based on the HTML tags and ensuring that whitespace is handled according to the context
    private string RenderNodeToString(INode node, bool preserveWhitespace, Uri baseUri)
    {
        var tempSb = new StringBuilder();
        void Walk(INode n, bool pw)
        {
            if (n is IElement el && el.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                tempSb.Append(Markup.Escape(n.TextContent));
                return;
            }
            RenderNodeToBuilder(n, tempSb, pw, baseUri, Walk);
        }
        Walk(node, preserveWhitespace);
        return tempSb.ToString();
    }

    // A helper method to render a DOM node and its children into the StringBuilder, applying appropriate formatting based on the HTML tags and ensuring that whitespace is handled according to the context
    private void RenderNodeToBuilder(INode node, StringBuilder sb, bool preserveWhitespace, Uri baseUri, Action<INode, bool> walkChildren)
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
            {
                if (sb.Length > 0 && sb[^1] != '\n')
                {
                    sb.AppendLine();
                }
            }

            if (tagName == "LI")
            {
                // To avoid floating • on empty list items
                if (element.TextContent?.Trim().Length > 0)
                {
                    sb.Append("• ");
                }
            }

            string? formatStyle = GetSpectreStyle(tagName);
            if (formatStyle != null) 
            {
                sb.Append($"[{formatStyle}]");
            }

            if (tagName == "IMG")
            {
                string? src = element.GetAttribute("src");
                string? alt = element.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(src))
                {
                    string altText = string.IsNullOrWhiteSpace(alt) ? "Image" : alt.Trim();
                    if (altText == "Image") {
                        // Skip rendering completely blank/meaningless decorative images
                        if (element.GetAttribute("role") == "presentation" || element.GetAttribute("aria-hidden") == "true" || string.IsNullOrWhiteSpace(alt))
                        {
                            return; 
                        }
                    }
                    string resolvedSrc = ResolveUrl(src, baseUri);
                    // Add newline if we are not at the beginning of a line
                    if (sb.Length > 0 && sb[^1] != '\n') sb.AppendLine();
                    // Avoid appending images if they don't have alt text unless they have link wrappers
                    if (altText != "Image" || element.ParentElement?.TagName == "A")
                    {
                        sb.Append($"[link={Markup.Escape(resolvedSrc)}]🖼️ {Markup.Escape(altText)}[/]\n");
                    }
                }
            }

            string? href = tagName.Equals("A", StringComparison.OrdinalIgnoreCase) ? element.GetAttribute("href") : null;
            bool addedLink = false;
            
            // Only render links if they contain actual text content (prevents empty links taking up space)
            string innerText = element.TextContent?.Trim() ?? "";
            bool hasTextContent = !string.IsNullOrWhiteSpace(innerText);
            
            // Do not output empty link wrapper if it only wraps a decorative image that was skipped
            bool hasValidImg = false;
            var imgNode = element.QuerySelector("img");
            if (imgNode != null)
            {
                string? alt = imgNode.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(alt) && alt != "Image")
                {
                    hasValidImg = true;
                }
                else if (imgNode.GetAttribute("role") != "presentation" && imgNode.GetAttribute("aria-hidden") != "true")
                {
                     hasValidImg = true;
                }
            }

            if (hasTextContent || hasValidImg)
            {
                if (!string.IsNullOrWhiteSpace(href))
                {
                    string resolvedHref = ResolveUrl(href, baseUri);
                    // Don't render empty links that are just spaces
                    if (!string.IsNullOrWhiteSpace(innerText) || hasValidImg)
                    {
                        // Clean inner text to prevent empty linked spaces
                        if (!hasValidImg && string.IsNullOrWhiteSpace(innerText)) 
                        {
                            addedLink = false;
                        } 
                        else 
                        {
                            sb.Append($"[link={Markup.Escape(resolvedHref)}]");
                            addedLink = true;
                        }
                    }
                }

                foreach (var child in element.ChildNodes)
                {
                    walkChildren(child, preserveWhitespace || isPre);
                }

                if (addedLink)
                {
                    sb.Append("[/]");
                }
            }
            else
            {
                // Just walk children without wrapping in link
                foreach (var child in element.ChildNodes)
                {
                    walkChildren(child, preserveWhitespace || isPre);
                }
            }

            if (formatStyle != null) 
            {
                sb.Append("[/]");
            }

            if (isBlock && tagName != "BODY") 
            {
                if (sb.Length > 0 && sb[^1] != '\n')
                {
                    sb.AppendLine();
                }
            }
        }
    }

    // A helper method to determine the appropriate Spectre.Console style string based on the HTML tag name, allowing for consistent formatting of headings, bold/italic text, and links in the rendered output
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
