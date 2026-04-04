using go2web.Commands.Enums;

namespace go2web.Configuration;

// The main application configuration record type that holds user preferences and settings for the go2web application
public record AppConfig
{
    public int MaxRedirects { get; init; } = 5;
    public bool AlwaysShowHeaders { get; init; } = false;
    public string DefaultAccept { get; init; } = "html";
    public string DefaultLanguage { get; init; } = "*";
    public SearchEngineType DefaultSearchEngine { get; init; } = SearchEngineType.DuckDuckGo;
}
