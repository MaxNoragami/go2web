using go2web.Http;

namespace go2web.Search;

// An interface that defines the contract for a search engine implementation
public interface ISearchEngine
{
    string Name { get; }
    Task<List<SearchResult>> SearchAsync(string query, IHttpClient client);
}
