using AngleSharp.Html.Parser;
using ConsoleAppFramework;
using go2web.Configuration;
using go2web.Http;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to search the term using a search engine and print top results.</summary>
    /// <param name="searchTerms">The search terms to query.</param>
    /// <param name="fullHeaders">-f, Show response headers.</param>
    /// <param name="redirects">-r, Number of redirects to follow.</param>
    /// <param name="lang">-l, Set the Accept-Language header (e.g. en, fr, ja).</param>
    /// <param name="engine">-e, The search engine to use (DuckDuckGo, Yahoo, Brave).</param>
    [Command("-s")]
    public async Task Search(
        [Argument] string[] searchTerms,
        bool fullHeaders = false,
        int? redirects = null,
        string? lang = null,
        SearchEngineType? engine = null)
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
        SearchEngineType activeEngine = engine ?? config.DefaultSearchEngine;

        var searchEngine = go2web.Search.SearchEngineFactory.Create(activeEngine);

        AnsiConsole.MarkupLine($"[dim]Searching {searchEngine.Name} for:[/] {Markup.Escape(query)}\n");

        try
        {
            IHttpClient client = new CachingHttpClientDecorator(new SocketHttpClient());
            
            var results = await searchEngine.SearchAsync(query, client);

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            var urls = new List<string>();
            var choices = new List<string>();

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                urls.Add(result.Url);

                string truncatedSnippet = result.Snippet.Length > 80 ? result.Snippet.Substring(0, 77) + "..." : result.Snippet;

                // Add title and dimmed snippet to the choices list for the interactive prompt
                choices.Add($"[bold white]{i + 1}.[/] {Markup.Escape(result.Title)}\n   [dim]{Markup.Escape(truncatedSnippet)}[/]");
            }

            choices.Add("[red]Exit[/]");

            if (AnsiConsole.Profile.Capabilities.Interactive)
            {
                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[bold]Top {results.Count} Results for '{Markup.Escape(query)}'[/]\nSelect a result to open, or [red]Exit[/] to quit:")
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
                AnsiConsole.MarkupLine("[dim]Interactive selection is disabled in this terminal environment. Showing top results:[/]");
                for (int i = 0; i < results.Count; i++)
                {
                    AnsiConsole.MarkupLine($"[bold white]{i + 1}.[/] [blue link={Markup.Escape(results[i].Url)}]{Markup.Escape(results[i].Title)}[/]");
                    AnsiConsole.MarkupLine($"   [green dim]{Markup.Escape(results[i].Url)}[/]");
                    AnsiConsole.MarkupLine($"   {Markup.Escape(results[i].Snippet)}\n");
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during search request:[/] {ex.Message}");
        }
    }
}
