using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Spectre.Console;
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
        "UL", "OL", "LI", "TR", "BR", "TABLE", "HEADER",
        "FOOTER", "MAIN", "SECTION", "ARTICLE", "ASIDE", "NAV", "BODY", "HR"
    };

    public string Render(string html)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);
        
        var sb = new StringBuilder();
        if (document.Body != null)
        {
            RenderNode(document.Body, sb, false);
        }
        
        // Clean up excess newlines (more than 2)
        string result = sb.ToString();
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim();
    }

    private void RenderNode(INode node, StringBuilder sb, bool preserveWhitespace)
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
                RenderNode(child, sb, preserveWhitespace || isPre);
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
