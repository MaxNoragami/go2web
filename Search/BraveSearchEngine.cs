using AngleSharp.Html.Parser;
using go2web.Http;
using System.Text.RegularExpressions;

namespace go2web.Search;

public class BraveSearchEngine : ISearchEngine
{
    public string Name => "Brave";

    public async Task<List<SearchResult>> SearchAsync(string query, IHttpClient client)
    {
        string encodedQuery = Uri.EscapeDataString(query);
        var uri = new Uri($"https://search.brave.com/search?q={encodedQuery}&source=web");
        
        var response = await client.GetAsync(uri, maxRedirects: 5, acceptHeader: "text/html", acceptLanguage: "*");
        var html = response.BodyString;

        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);

        var mainContainers = document.QuerySelectorAll("div[data-type='web']").ToList();
        if (mainContainers.Count == 0)
        {
            // Fallback for different DOM structure
            mainContainers = document.QuerySelectorAll(".snippet").ToList();
        }

        var results = new List<SearchResult>();

        foreach (var container in mainContainers)
        {
            var linkNode = container.QuerySelector("a.svelte-14r20fy") ?? container.QuerySelector("a");
            if (linkNode == null) continue;

            var titleNode = container.QuerySelector(".search-snippet-title") ?? container.QuerySelector(".title") ?? container.QuerySelector("h3") ?? linkNode;
            var snippetNode = container.QuerySelector(".generic-snippet") ?? container.QuerySelector(".snippet-content") ?? container.QuerySelector(".snippet-description");

            string title = titleNode != null ? Regex.Replace(titleNode.TextContent, @"\s+", " ").Trim() : Regex.Replace(linkNode.TextContent, @"\s+", " ").Trim();
            string url = linkNode.GetAttribute("href") ?? "";
            
            if (url.StartsWith("/"))
            {
                url = "https://search.brave.com" + url;
            }

            string snippet = snippetNode != null ? Regex.Replace(snippetNode.TextContent, @"\s+", " ").Trim() : "";

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url) && url.StartsWith("http") && !url.Contains("search.brave.com/search"))
            {
                // Only add if not duplicate
                if (!results.Any(r => r.Url == url))
                {
                    results.Add(new SearchResult(title, url, snippet));
                }
            }
            if (results.Count >= 10) break;
        }

        return results;
    }
}
