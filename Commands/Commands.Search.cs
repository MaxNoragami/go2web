using ConsoleAppFramework;
using Spectre.Console;
using go2web.Configuration;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to search the term using a search engine and print top results.</summary>
    /// <param name="searchTerm">The search term, multiple terms must be quoted.</param>
    /// <param name="fullHeaders">-f, Show response headers.</param>
    /// <param name="redirects">-r, Number of redirects to follow.</param>
    [Command("-s")]
    public async Task Search(
        [Argument] string searchTerm,
        bool fullHeaders = false,
        int? redirects = null)
    {
        var config = ConfigLoader.Load();
        int maxRedirects = redirects ?? config.MaxRedirects;
        fullHeaders = fullHeaders || config.AlwaysShowHeaders;

        AnsiConsole.MarkupLine($"[green]Searching for:[/] {searchTerm} (max redirects: {maxRedirects})");
    }
}