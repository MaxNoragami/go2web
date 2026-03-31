using ConsoleAppFramework;
using Spectre.Console;
using go2web.Http;
using go2web.Configuration;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to the specified URL and print the response.</summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="fullHeaders">-f, Show response headers.</param>
    /// <param name="redirects">-r, Number of redirects to follow.</param>
    [Command("-u")]
    public async Task Url(
        [Argument] string url,
        bool fullHeaders = false,
        int? redirects = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid URL provided: {url}");
            return;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Only HTTP and HTTPS URLs are currently supported. Provided: {uri.Scheme}");
            return;
        }

        var config = ConfigLoader.Load();
        int maxRedirects = redirects ?? config.MaxRedirects;
        fullHeaders = fullHeaders || config.AlwaysShowHeaders;

        AnsiConsole.MarkupLine($"[dim]Fetching {uri}...[/]");

        try
        {
            var client = new SocketHttpClient();
            var response = await client.GetAsync(uri, maxRedirects, (statusCode, redirectUri) =>
            {
                AnsiConsole.MarkupLine($"[cyan]{statusCode}[/] [dim]-> redirecting to {redirectUri}...[/]");
            });

            AnsiConsole.MarkupLine($"\n[bold green]HTTP {response.StatusCode} {response.ReasonPhrase}[/]\n");

            if (fullHeaders)
            {
                var table = new Table().Border(TableBorder.Rounded).Title("Response Headers");
                table.AddColumn("Header");
                table.AddColumn("Value");
                foreach (var header in response.Headers)
                {
                    table.AddRow(new Markup($"[cyan]{header.Key}[/]"), new Markup(header.Value));
                }
                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }
            
            Console.WriteLine(response.BodyString);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during HTTP request:[/] {ex.Message}");
        }
    }
}
