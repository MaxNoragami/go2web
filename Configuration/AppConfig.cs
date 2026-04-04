using go2web.Commands.Enums;

namespace go2web.Configuration;

public class AppConfig
{
    public int MaxRedirects { get; init; } = 5;
    public bool AlwaysShowHeaders { get; init; } = false;
    public string DefaultAccept { get; init; } = "html";
    public string DefaultLanguage { get; init; } = "*";
    public SearchEngineType DefaultSearchEngine { get; init; } = SearchEngineType.DuckDuckGo;
}
