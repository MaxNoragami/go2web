using AngleSharp.Html.Parser;
using System.Text.RegularExpressions;
using System.Web;

namespace go2web.Search;

public class YahooSearchEngine : ISearchEngine
{
    public string Name => "Yahoo";

    public Uri BuildQueryUri(string query)
    {
        string encodedQuery = HttpUtility.UrlEncode(query);
        return new Uri($"https://search.yahoo.com/search?p={encodedQuery}");
    }

    public List<SearchResult> ParseResults(string html)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);

        // Yahoo results are typically within .compTitle a and .compText
        var resultContainers = document.QuerySelectorAll(".algo-sr:not(.ad), .algo").Take(10).ToList();
        
        // Fallback if structure is slightly different
        if (resultContainers.Count == 0)
        {
            resultContainers = document.QuerySelectorAll(".dd.algo").Take(10).ToList();
        }

        var results = new List<SearchResult>();

        foreach (var container in resultContainers)
        {
            var linkNode = container.QuerySelector("h3.title a") ?? container.QuerySelector(".compTitle a") ?? container.QuerySelector("a");
            var snippetNode = container.QuerySelector(".compText") ?? container.QuerySelector(".ab_ttw");

            if (linkNode == null) continue;

            string title = Regex.Replace(linkNode.TextContent, @"\s+", " ").Trim();
            string url = linkNode.GetAttribute("href") ?? "";
            
            // Un-escape Yahoo tracking urls if necessary: https://r.search.yahoo.com/.../RU=https://...
            if (url.Contains("/RU="))
            {
                try
                {
                    var match = Regex.Match(url, @"/RU=([^/]+)/");
                    if (match.Success)
                    {
                        url = HttpUtility.UrlDecode(match.Groups[1].Value);
                    }
                }
                catch { }
            }

            string snippet = snippetNode != null ? Regex.Replace(snippetNode.TextContent, @"\s+", " ").Trim() : "";

            results.Add(new SearchResult(title, url, snippet));
            if (results.Count >= 10) break;
        }
        
        // Final fallback if container selectors completely miss (simple extraction)
        if (results.Count == 0)
        {
            var links = document.QuerySelectorAll(".compTitle a").Take(10).ToList();
            var snippets = document.QuerySelectorAll(".compText").Take(10).ToList();
            for (int i = 0; i < links.Count; i++)
            {
                string title = Regex.Replace(links[i].TextContent, @"\s+", " ").Trim();
                string url = links[i].GetAttribute("href") ?? "";
                string snippet = i < snippets.Count ? Regex.Replace(snippets[i].TextContent, @"\s+", " ").Trim() : "";
                results.Add(new SearchResult(title, url, snippet));
            }
        }

        return results;
    }
}