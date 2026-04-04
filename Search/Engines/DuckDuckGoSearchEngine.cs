using AngleSharp.Html.Parser;
using go2web.Http;
using System.Text.RegularExpressions;

namespace go2web.Search.Engines;

// An implementation of the ISearchEngine interface for performing searches using the DuckDuckGo search engine
public class DuckDuckGoSearchEngine : ISearchEngine
{
    public string Name => "DuckDuckGo";

    public async Task<List<SearchResult>> SearchAsync(string query, IHttpClient client)
    {
        // Encode the search query and construct the DuckDuckGo search URL
        string encodedQuery = Uri.EscapeDataString(query);
        var uri = new Uri($"https://html.duckduckgo.com/html/?q={encodedQuery}");

        // Perform an HTTP GET request to the DuckDuckGo search URL and retrieve the HTML response
        var response = await client.GetAsync(uri, maxRedirects: 5, acceptHeader: "text/html", acceptLanguage: "*");
        var html = response.BodyString;

        // Parse the HTML response using AngleSharp to extract search results, handling potential variations in the DOM structure and ensuring that only valid results are included in the final list of SearchResult objects
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
                    var redirectUri = new Uri(href.StartsWith("//") ? "https:" + href : href);
                    var redirectResponse = await client.GetAsync(redirectUri, maxRedirects: 0);
                    
                    if (redirectResponse.IsRedirect)
                    {
                        var location = redirectResponse.GetHeader("Location");
                        if (!string.IsNullOrEmpty(location))
                        {
                            url = location;
                        }
                    }
                    else if (redirectResponse.StatusCode == 200)
                    {
                        var match = Regex.Match(redirectResponse.BodyString, @"window\.parent\.location\.replace\(""([^""]+)""\)");
                        if (match.Success)
                        {
                            url = match.Groups[1].Value;
                        }
                        else
                        {
                            match = Regex.Match(redirectResponse.BodyString, @"URL=([^""'>\s]+)");
                            if (match.Success)
                            {
                                url = match.Groups[1].Value;
                            }
                        }
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
