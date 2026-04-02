using AngleSharp.Html.Parser;
using System.Text.RegularExpressions;
using System.Web;

namespace go2web.Search;

public class DuckDuckGoSearchEngine : ISearchEngine
{
    public string Name => "DuckDuckGo";

    public Uri BuildQueryUri(string query)
    {
        string encodedQuery = HttpUtility.UrlEncode(query);
        return new Uri($"https://html.duckduckgo.com/html/?q={encodedQuery}");
    }

    public List<SearchResult> ParseResults(string html)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);

        var resultLinks = document.QuerySelectorAll("a.result__a").Take(10).ToList();
        var resultSnippets = document.QuerySelectorAll("a.result__snippet").Take(10).ToList();

        var results = new List<SearchResult>();

        for (int i = 0; i < resultLinks.Count; i++)
        {
            var linkNode = resultLinks[i];
            var snippetNode = i < resultSnippets.Count ? resultSnippets[i] : null;

            string title = Regex.Replace(linkNode.TextContent, @"\s+", " ").Trim();
            string href = linkNode.GetAttribute("href") ?? "";
            string url = href;

            if (href.Contains("uddg="))
            {
                try
                {
                    var uri = new Uri(href.StartsWith("//") ? "https:" + href : href);
                    var queryDict = HttpUtility.ParseQueryString(uri.Query);
                    var uddg = queryDict["uddg"];
                    if (!string.IsNullOrEmpty(uddg))
                    {
                        url = uddg;
                    }
                }
                catch { }
            }

            if (url.StartsWith("//")) url = "https:" + url;

            string snippet = snippetNode != null ? Regex.Replace(snippetNode.TextContent, @"\s+", " ").Trim() : "";

            results.Add(new SearchResult(title, url, snippet));
        }

        return results;
    }
}