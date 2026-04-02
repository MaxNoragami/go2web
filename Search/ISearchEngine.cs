namespace go2web.Search;

public interface ISearchEngine
{
    string Name { get; }
    Uri BuildQueryUri(string query);
    List<SearchResult> ParseResults(string html);
}