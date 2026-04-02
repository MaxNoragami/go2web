using go2web.Http;

namespace go2web.Search;

public interface ISearchEngine
{
    string Name { get; }
    Task<List<SearchResult>> SearchAsync(string query, IHttpClient client);
}
