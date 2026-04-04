using ConsoleAppFramework;
using Spectre.Console;
using Spectre.Console.Json;
using go2web.Http;
using go2web.Http.Clients;
using go2web.Configuration;
using go2web.Rendering;
using go2web.Commands.Enums;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to the specified URL and print the response.</summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="fullHeaders">-f, Show response headers.</param>
    /// <param name="redirects">-r, Number of redirects to follow.</param>
    /// <param name="accept">-a, Set the Accept header for content negotiation.</param>
    /// <param name="lang">-l, Set the Accept-Language header (e.g. en, fr, ja).</param>
    [Command("-u")]
    public async Task Url(
        [Argument] string url,
        bool fullHeaders = false,
        int? redirects = null,
        AcceptType? accept = null,
        string? lang = null)
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

        // Load configuration for defaults and overrides
        var config = ConfigLoader.Load();
        int maxRedirects = redirects ?? config.MaxRedirects;
        fullHeaders = fullHeaders || config.AlwaysShowHeaders;
        
        // Determine the active Accept header value based on the command-line argument or configuration default
        string activeLang = lang ?? config.DefaultLanguage ?? "*";
        
        AcceptType activeAccept = accept ?? (Enum.TryParse<AcceptType>(config.DefaultAccept, true, out var parsedAccept) ? parsedAccept : AcceptType.Html);

        AnsiConsole.MarkupLine($"[dim]Fetching {uri}...[/]");

        string acceptHeaderValue = activeAccept switch
        {
            AcceptType.Json => "application/json",
            AcceptType.Plain => "text/plain",
            AcceptType.Html => "text/html",
            _ => "text/html"
        };

        try
        {
            // Use a caching HTTP client to perform the GET request to the specified URL with the appropriate headers and redirect handling
            IHttpClient client = new CachingHttpClientDecorator(new SocketHttpClient());
            var response = await client.GetAsync(uri, maxRedirects, acceptHeaderValue, activeLang, (statusCode, redirectUri) =>
            {
                AnsiConsole.MarkupLine($"[cyan]{statusCode}[/] [dim]-> redirecting to {redirectUri}...[/]");
            });

            AnsiConsole.MarkupLine($"\n[bold green]HTTP {response.StatusCode} {response.ReasonPhrase}[/]\n");

            // If the user requested to see full headers, display them in a formatted table
            if (fullHeaders)
            {
                var table = new Table().Border(TableBorder.Rounded).Title("Response Headers");
                table.AddColumn("Header");
                table.AddColumn("Value");
                foreach (var header in response.Headers)
                {
                    table.AddRow(new Markup($"[cyan]{Markup.Escape(header.Key)}[/]"), new Text(header.Value));
                }
                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }
            
            // Determine the content type of the response and choose how to display it based on the active Accept header and actual content type
            var contentType = response.GetHeader("Content-Type") ?? "text/html";
            bool isJsonContent = contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
            bool isHtmlContent = contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase);

            if (activeAccept == AcceptType.Json && !isJsonContent)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] You requested JSON, but the server returned a different content type.");
                activeAccept = isHtmlContent ? AcceptType.Html : AcceptType.Plain;
            }
            else if (activeAccept == AcceptType.Html && !isHtmlContent)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] You requested HTML, but the server returned a different content type.");
                activeAccept = isJsonContent ? AcceptType.Json : AcceptType.Plain;
            }

            // Display the response body according to the determined content type and active Accept header
            if (isJsonContent && activeAccept != AcceptType.Plain)
            {
                // Try to pretty-print JSON content
                try
                {
                    AnsiConsole.Write(new JsonText(response.BodyString));
                    AnsiConsole.WriteLine();
                }
                catch
                {
                    Console.WriteLine(response.BodyString);
                }
            }
            else if ((isHtmlContent && activeAccept != AcceptType.Plain) || activeAccept == AcceptType.Html)
            {
                // Render HTML content using the HtmlRenderer to convert it into ANSI-formatted text for terminal display
                var renderer = new HtmlRenderer();
                var renderables = renderer.Render(response.BodyString, uri);
                foreach (var r in renderables)
                {
                    AnsiConsole.Write(r);
                    if (r is Table)
                    {
                        AnsiConsole.WriteLine();
                    }
                }
            }
            else
            {
                Console.WriteLine(response.BodyString);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during HTTP request:[/] {ex.Message}");
        }
    }
}
