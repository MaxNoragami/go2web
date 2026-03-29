using ConsoleAppFramework;
using Spectre.Console;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to the specified URL and print the response.</summary>
    /// <param name="url">The URL to fetch.</param>
    [Command("-u")]
    public async Task Url(
        [Argument] string url)
    {
        AnsiConsole.MarkupLine($"[green]Fetching URL:[/] {url}");
    }
}
