using go2web.Commands.Enums;
using go2web.Search.Engines;

namespace go2web.Search;

public static class SearchEngineFactory
{
    public static ISearchEngine Create(SearchEngineType type)
    {
        return type switch
        {
            SearchEngineType.DuckDuckGo => new DuckDuckGoSearchEngine(),
            SearchEngineType.Yahoo => new YahooSearchEngine(),
            SearchEngineType.Brave => new BraveSearchEngine(),
            _ => new DuckDuckGoSearchEngine()
        };
    }
}