using AngleSharp.Html.Parser;
using ConsoleAppFramework;
using go2web.Configuration;
using go2web.Http;
using Spectre.Console;
using System.Text.RegularExpressions;
using System.Web;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to search the term using a search engine and print top results.</summary>
    /// <param name="searchTerms">The search terms to query.</param>
    /// <param name="fullHeaders">-f, Show response headers.</param>
    /// <param name="redirects">-r, Number of redirects to follow.</param>
    /// <param name="lang">-l, Set the Accept-Language header (e.g. en, fr, ja).</param>
    [Command("-s")]
    public async Task Search(
        [Argument] string[] searchTerms,
        bool fullHeaders = false,
        int? redirects = null,
        string? lang = null)
    {
        string query = string.Join(" ", searchTerms);
        if (string.IsNullOrWhiteSpace(query))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] You must provide a search term.");
            return;
        }

        var config = ConfigLoader.Load();
        int maxRedirects = redirects ?? config.MaxRedirects;
        fullHeaders = fullHeaders || config.AlwaysShowHeaders;
        string activeLang = lang ?? config.DefaultLanguage ?? "*";

        AnsiConsole.MarkupLine($"[dim]Searching DuckDuckGo for:[/] {Markup.Escape(query)}\n");

        try
        {
            IHttpClient client = new CachingHttpClientDecorator(new SocketHttpClient());
            
            string encodedQuery = HttpUtility.UrlEncode(query);
            var searchUri = new Uri($"https://html.duckduckgo.com/html/?q={encodedQuery}");

            var response = await client.GetAsync(
                searchUri, 
                maxRedirects, 
                "text/html", 
                activeLang, 
                (statusCode, redirectUri) =>
                {
                    if (fullHeaders)
                    {
                        AnsiConsole.MarkupLine($"[cyan]{statusCode}[/] [dim]-> redirecting to {redirectUri}...[/]");
                    }
                });

            if (fullHeaders)
            {
                AnsiConsole.MarkupLine($"\n[bold green]HTTP {response.StatusCode} {response.ReasonPhrase}[/]\n");
                var headerTable = new Table().Border(TableBorder.Rounded).Title("Response Headers");
                headerTable.AddColumn("Header");
                headerTable.AddColumn("Value");
                foreach (var header in response.Headers)
                {
                    headerTable.AddRow(new Markup($"[cyan]{Markup.Escape(header.Key)}[/]"), new Text(header.Value));
                }
                AnsiConsole.Write(headerTable);
                AnsiConsole.WriteLine();
            }

            var parser = new HtmlParser();
            using var document = parser.ParseDocument(response.BodyString);

            var resultLinks = document.QuerySelectorAll("a.result__a").Take(10).ToList();
            var resultSnippets = document.QuerySelectorAll("a.result__snippet").Take(10).ToList();

            if (resultLinks.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            var urls = new List<string>();
            var choices = new List<string>();

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

                urls.Add(url);

                string truncatedSnippet = snippet.Length > 80 ? snippet.Substring(0, 77) + "..." : snippet;

                // Add title and dimmed snippet to the choices list for the interactive prompt
                choices.Add($"[bold white]{i + 1}.[/] {Markup.Escape(title)}\n   [dim]{Markup.Escape(truncatedSnippet)}[/]");
            }

            choices.Add("[red]Exit[/]");

            if (AnsiConsole.Profile.Capabilities.Interactive)
            {
                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[bold]Top {resultLinks.Count} Results for '{Markup.Escape(query)}'[/]\nSelect a result to open, or [red]Exit[/] to quit:")
                        .PageSize(22) // 10 results * 2 lines + 1 exit
                        .AddChoices(choices));

                if (selection != "[red]Exit[/]")
                {
                    int selectedIndex = choices.IndexOf(selection);
                    if (selectedIndex >= 0 && selectedIndex < urls.Count)
                    {
                        string selectedUrl = urls[selectedIndex];
                        AnsiConsole.MarkupLine($"\n[dim]Accessing {Markup.Escape(selectedUrl)}...[/]\n");
                        
                        await Url(selectedUrl, fullHeaders, maxRedirects, AcceptType.Html, activeLang);
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Interactive selection is disabled in this terminal environment.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during search request:[/] {ex.Message}");
        }
    }
}