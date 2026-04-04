using AngleSharp.Html.Parser;
using go2web.Http;
using System.Text.RegularExpressions;

namespace go2web.Search.Engines;

// An implementation of the ISearchEngine interface for performing searches using the Yahoo search engine
public class YahooSearchEngine : ISearchEngine
{
    public string Name => "Yahoo";

    public async Task<List<SearchResult>> SearchAsync(string query, IHttpClient client)
    {
        // Encode the search query and construct the Yahoo search URL
        string encodedQuery = Uri.EscapeDataString(query);
        var uri = new Uri($"https://search.yahoo.com/search?p={encodedQuery}");

        // Perform an HTTP GET request to the Yahoo search URL and retrieve the HTML response
        var response = await client.GetAsync(uri, maxRedirects: 5, acceptHeader: "text/html", acceptLanguage: "*");
        var html = response.BodyString;

        // Parse the HTML response using AngleSharp to extract search results, handling potential variations in the DOM structure and ensuring that only valid results are included in the final list of SearchResult objects
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);

        // Yahoo results within .compTitle a and .compText
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

            var titleNode = linkNode.QuerySelector("h3.title, .title, h3") ?? container.QuerySelector("h3.title") ?? linkNode;
            string title = Regex.Replace(titleNode.TextContent, @"\s+", " ").Trim();
            string url = linkNode.GetAttribute("href") ?? "";
            
            if (url.Contains("/RU="))
            {
                try
                {
                    var redirectUri = new Uri(url);
                    var redirectResponse = await client.GetAsync(redirectUri, maxRedirects: 0);
                    
                    if (redirectResponse.IsRedirect)
                    {
                        var location = redirectResponse.GetHeader("Location");
                        if (!string.IsNullOrEmpty(location))
                        {
                            url = location;
                        }
                    }
                }
                catch { }
            }

            string snippet = snippetNode != null ? Regex.Replace(snippetNode.TextContent, @"\s+", " ").Trim() : "";

            results.Add(new SearchResult(title, url, snippet));
            if (results.Count >= 10) break;
        }
        
        // Fallback if container selectors completely miss
        if (results.Count == 0)
        {
            var links = document.QuerySelectorAll(".compTitle a").Take(10).ToList();
            var snippets = document.QuerySelectorAll(".compText").Take(10).ToList();
            for (int i = 0; i < links.Count; i++)
            {
                var linkNode = links[i];
                var titleNode = linkNode.QuerySelector("h3.title, .title, h3") ?? linkNode;
                string title = Regex.Replace(titleNode.TextContent, @"\s+", " ").Trim();
                string url = linkNode.GetAttribute("href") ?? "";
                
                if (url.Contains("/RU="))
                {
                    try
                    {
                        var redirectUri = new Uri(url);
                        var redirectResponse = await client.GetAsync(redirectUri, maxRedirects: 0);
                        
                        if (redirectResponse.IsRedirect)
                        {
                            var location = redirectResponse.GetHeader("Location");
                            if (!string.IsNullOrEmpty(location))
                            {
                                url = location;
                            }
                        }
                    }
                    catch { }
                }

                string snippet = i < snippets.Count ? Regex.Replace(snippets[i].TextContent, @"\s+", " ").Trim() : "";
                results.Add(new SearchResult(title, url, snippet));
            }
        }

        return results;
    }
}
