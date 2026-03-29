using ConsoleAppFramework;
using Spectre.Console;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Make an HTTP request to search the term using a search engine and print top results.</summary>
    /// <param name="searchTerm">The search term, multiple terms must be quoted.</param>
    [Command("-s")]
    public async Task Search(
        [Argument] string searchTerm)
    {
        AnsiConsole.MarkupLine($"[green]Searching for:[/] {searchTerm}");
    }
}